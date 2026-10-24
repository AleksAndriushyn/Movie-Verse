using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MovieVerse.Api.Data;
using MovieVerse.Api.Payments;

namespace MovieVerse.Api.Tickets;

public sealed class SeatsTakenException() : Exception("Some of the selected seats were just taken.");

/// <summary>
/// Seat holds and their lifecycle. A pending booking holds its seats until <see cref="Booking.ExpiresAtUtc"/>;
/// expired holds are released lazily, the next time anyone touches that showtime, so no background job is needed.
/// </summary>
public sealed class BookingService(AppDbContext db, IPaymentGateway payments, ILogger<BookingService> logger)
{
    public const int MaxSeatsPerBooking = 10;

    /// <summary>Stripe's checkout pages live at least 30 minutes; the hold outlasts the page by a margin.</summary>
    public static readonly TimeSpan CheckoutLifetime = TimeSpan.FromMinutes(31);
    public static readonly TimeSpan HoldLifetime = TimeSpan.FromMinutes(33);

    /// <summary>Bookings close shortly before the movie starts.</summary>
    public static readonly TimeSpan SalesCloseBeforeStart = TimeSpan.FromMinutes(15);

    /// <summary>A paid ticket can be refunded until an hour before the show.</summary>
    public static readonly TimeSpan RefundsCloseBeforeStart = TimeSpan.FromHours(1);

    public static bool IsBookable(Showtime showtime) => showtime.StartsAtUtc - SalesCloseBeforeStart > DateTime.UtcNow;

    public async Task ReleaseExpiredHoldsAsync(int showtimeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = db.Bookings.Where(b =>
            b.ShowtimeId == showtimeId && b.Status == BookingStatus.Pending && b.ExpiresAtUtc <= now);

        await db.BookingSeats.Where(s => expired.Any(b => b.Id == s.BookingId)).ExecuteDeleteAsync(ct);
        await expired.ExecuteUpdateAsync(set => set.SetProperty(b => b.Status, BookingStatus.Expired), ct);
    }

    /// <summary>
    /// Holds the seats for the user, replacing any hold they already had on this showtime.
    /// Throws <see cref="SeatsTakenException"/> when another booking got any of them first.
    /// </summary>
    public async Task<Booking> HoldAsync(int userId, Showtime showtime, IReadOnlyList<Seat> seats, CancellationToken ct)
    {
        Booking? booking = null;
        var replaced = new List<string>();

        try
        {
            // user transactions must run inside the retrying strategy the SQL Server provider is configured with
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                replaced.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);

                await ReleaseExpiredHoldsAsync(showtime.Id, ct);

                var previous = db.Bookings.Where(b =>
                    b.UserId == userId && b.ShowtimeId == showtime.Id && b.Status == BookingStatus.Pending);
                replaced.AddRange(await previous
                    .Where(b => b.StripeSessionId != null)
                    .Select(b => b.StripeSessionId!)
                    .ToListAsync(ct));
                await db.BookingSeats.Where(s => previous.Any(b => b.Id == s.BookingId)).ExecuteDeleteAsync(ct);
                await previous.ExecuteUpdateAsync(set => set.SetProperty(b => b.Status, BookingStatus.Cancelled), ct);

                var now = DateTime.UtcNow;
                booking = new Booking
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ShowtimeId = showtime.Id,
                    Status = BookingStatus.Pending,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = now + HoldLifetime,
                    SeatLabels = string.Join(", ", seats.Select(s => $"{s.Row}{s.Number}")),
                    Seats = seats.Select(seat => new BookingSeat
                    {
                        ShowtimeId = showtime.Id,
                        SeatId = seat.Id,
                        PriceCents = PriceOf(showtime, seat.Type),
                    }).ToList(),
                };
                booking.TotalCents = booking.Seats.Sum(s => s.PriceCents);
                db.Bookings.Add(booking);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
        }
        catch (DbUpdateException)
        {
            // the unique (showtime, seat) index: someone else holds or bought one of these seats
            db.ChangeTracker.Clear();
            throw new SeatsTakenException();
        }

        // the replaced holds' payment pages must not be payable any more
        foreach (var sessionId in replaced)
        {
            await TryExpireCheckoutAsync(sessionId, ct);
        }

        return booking!;
    }

    public static int PriceOf(Showtime showtime, SeatType type) =>
        type == SeatType.Vip ? showtime.VipPriceCents : showtime.StandardPriceCents;

    /// <summary>Records a successful payment. Idempotent: webhooks and the return page may both report it.</summary>
    public async Task MarkPaidAsync(CheckoutState checkout, CancellationToken ct)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.StripeSessionId == checkout.SessionId, ct);
        if (booking is null)
        {
            return;
        }

        // only a still-pending booking becomes paid; a concurrent cancel or expiry wins otherwise
        var paid = await db.Bookings
            .Where(b => b.Id == booking.Id && b.Status == BookingStatus.Pending)
            .ExecuteUpdateAsync(set => set
                .SetProperty(b => b.Status, BookingStatus.Paid)
                .SetProperty(b => b.PaidAtUtc, DateTime.UtcNow)
                .SetProperty(b => b.StripePaymentIntentId, checkout.PaymentIntentId)
                .SetProperty(b => b.TicketCode, NewTicketCode())
                .SetProperty(b => b.CheckoutUrl, (string?)null), ct);
        if (paid > 0)
        {
            return;
        }

        await db.Entry(booking).ReloadAsync(ct);
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Expired)
        {
            // paid after the seats were released: give the money back
            logger.LogWarning("Booking {BookingId} was paid while {Status}; refunding", booking.Id, booking.Status);
            if (checkout.PaymentIntentId is not null)
            {
                await payments.RefundAsync(checkout.PaymentIntentId, ct);
            }
            await db.Bookings
                .Where(b => b.Id == booking.Id && b.Status == booking.Status)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(b => b.Status, BookingStatus.Refunded)
                    .SetProperty(b => b.StripePaymentIntentId, checkout.PaymentIntentId), ct);
        }
    }

    /// <summary>
    /// Moves a booking from the status it was read with to <paramref name="status"/> and frees its seats, in one
    /// transaction. Returns false (and reloads the booking) if another request changed it first, e.g. a webhook
    /// marked it paid while this request was expiring it.
    /// </summary>
    public async Task<bool> ReleaseAsync(Booking booking, BookingStatus status, CancellationToken ct)
    {
        var from = booking.Status;
        var released = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var updated = await db.Bookings
                .Where(b => b.Id == booking.Id && b.Status == from)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(b => b.Status, status)
                    .SetProperty(b => b.CheckoutUrl, (string?)null), ct);
            if (updated == 0)
            {
                return false;
            }

            await db.BookingSeats.Where(s => s.BookingId == booking.Id).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });

        if (released)
        {
            booking.Status = status;
            booking.CheckoutUrl = null;
        }
        else
        {
            await db.Entry(booking).ReloadAsync(ct);
        }

        return released;
    }

    /// <summary>
    /// Brings a pending booking up to date with Stripe, for when the webhook hasn't arrived
    /// (or can't, like on a developer machine).
    /// </summary>
    public async Task ReconcileAsync(Booking booking, CancellationToken ct)
    {
        if (booking.Status != BookingStatus.Pending || booking.StripeSessionId is null)
        {
            return;
        }

        var checkout = await payments.GetCheckoutAsync(booking.StripeSessionId, ct);
        if (checkout.Paid)
        {
            await MarkPaidAsync(checkout, ct);
            await db.Entry(booking).ReloadAsync(ct);
        }
        else if (checkout.Expired || booking.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await ReleaseAsync(booking, BookingStatus.Expired, ct);
        }
    }

    public async Task TryExpireCheckoutAsync(string sessionId, CancellationToken ct)
    {
        try
        {
            await payments.ExpireCheckoutAsync(sessionId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // the hold is released either way, and the page expires on its own
            logger.LogWarning(ex, "Couldn't expire checkout session {SessionId}", sessionId);
        }
    }

    // 8 characters without look-alikes (0/O, 1/I/L), easy to read out at the door
    private static string NewTicketCode() =>
        RandomNumberGenerator.GetString("ABCDEFGHJKMNPQRSTUVWXYZ23456789", 8);
}
