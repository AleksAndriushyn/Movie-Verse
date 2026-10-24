using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MovieVerse.Api.Auth;
using MovieVerse.Api.Data;
using MovieVerse.Api.Payments;

namespace MovieVerse.Api.Tickets;

public sealed record CreateBookingRequest(
    [property: Required, MinLength(1), MaxLength(BookingService.MaxSeatsPerBooking)] int[] SeatIds);

public sealed record CreatedBooking(Guid BookingId, string CheckoutUrl);

public sealed record BookingDto(
    Guid Id,
    BookingStatus Status,
    int ShowtimeId,
    int MovieId,
    string MovieTitle,
    string? PosterPath,
    CinemaDto Cinema,
    string HallName,
    DateTime StartsAt,
    string SeatLabels,
    int TotalCents,
    string Currency,
    string? TicketCode,
    DateTime? HoldExpiresAt,
    string? CheckoutUrl,
    bool CanCancel)
{
    public static BookingDto From(Booking booking)
    {
        var showtime = booking.Showtime!;
        var isPending = booking.Status == BookingStatus.Pending;
        return new BookingDto(
            booking.Id,
            booking.Status,
            showtime.Id,
            showtime.MovieId,
            showtime.MovieTitle,
            showtime.PosterPath,
            ShowtimeEndpoints.CinemaOfHall(showtime.HallId),
            ShowtimeEndpoints.HallName(showtime.HallId),
            showtime.StartsAtUtc,
            booking.SeatLabels,
            booking.TotalCents,
            Booking.Currency,
            booking.Status == BookingStatus.Paid ? booking.TicketCode : null,
            isPending ? booking.ExpiresAtUtc : null,
            isPending ? booking.CheckoutUrl : null,
            isPending || (booking.Status == BookingStatus.Paid
                && showtime.StartsAtUtc - BookingService.RefundsCloseBeforeStart > DateTime.UtcNow));
    }
}

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/showtimes/{showtimeId:int}/bookings", async (
                int showtimeId,
                CreateBookingRequest request,
                HttpContext http,
                ClaimsPrincipal user,
                AppDbContext db,
                BookingService bookings,
                IPaymentGateway payments,
                ILogger<BookingService> logger,
                CancellationToken ct) =>
        {
            var seatIds = request.SeatIds.Distinct().ToArray();
            var showtime = await db.Showtimes.SingleOrDefaultAsync(s => s.Id == showtimeId, ct);
            if (showtime is null)
            {
                return Results.NotFound();
            }

            if (!BookingService.IsBookable(showtime))
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Sales for this showtime have closed");
            }

            var seats = await db.Seats
                .Where(s => s.HallId == showtime.HallId && seatIds.Contains(s.Id))
                .OrderBy(s => s.Row)
                .ThenBy(s => s.Number)
                .ToListAsync(ct);
            if (seats.Count != seatIds.Length)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["seatIds"] = ["Some seats don't exist in this hall."],
                });
            }

            Booking booking;
            try
            {
                booking = await bookings.HoldAsync(user.GetUserId(), showtime, seats, ct);
            }
            catch (SeatsTakenException ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Seats taken", detail: ex.Message);
            }

            var startsLocal = TimeZoneInfo.ConvertTimeFromUtc(showtime.StartsAtUtc, ShowtimeScheduler.Zone)
                .ToString("ddd, MMM d, h:mm tt", CultureInfo.InvariantCulture);
            var where = $"{ShowtimeEndpoints.CinemaOfHall(showtime.HallId).Name}, {startsLocal}";
            var lines = seats
                .GroupBy(s => s.Type)
                .Select(group => new CheckoutLine(
                    $"{showtime.MovieTitle}: {SeatTypeName(group.Key)} ({where})",
                    BookingService.PriceOf(showtime, group.Key),
                    group.Count()))
                .ToList();

            // in production the public-origin middleware makes this the site's address
            var origin = $"{http.Request.Scheme}://{http.Request.Host}";
            CheckoutSession checkout;
            try
            {
                checkout = await payments.CreateCheckoutAsync(new CheckoutRequest(
                    booking.Id,
                    lines,
                    user.FindFirstValue(ClaimTypes.Email),
                    $"{origin}/tickets/{booking.Id}",
                    $"{origin}/showtimes/{showtime.Id}?cancelledBooking={booking.Id}",
                    DateTime.UtcNow + BookingService.CheckoutLifetime), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Couldn't start checkout for booking {BookingId}", booking.Id);
                await bookings.ReleaseAsync(booking, BookingStatus.Cancelled, ct);
                return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Payments are unavailable right now");
            }

            await db.Bookings
                .Where(b => b.Id == booking.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(b => b.StripeSessionId, checkout.Id)
                    .SetProperty(b => b.CheckoutUrl, checkout.Url), ct);

            return Results.Ok(new CreatedBooking(booking.Id, checkout.Url));
        })
            .RequireAuthorization()
            .WithTags("Tickets")
            .WithName("CreateBooking");

        var mine = app.MapGroup("/api/me/bookings").WithTags("Tickets").RequireAuthorization();

        // Tickets and active holds; expired and abandoned holds are left out
        mine.MapGet("/", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var now = DateTime.UtcNow;
            var list = await db.Bookings
                .Include(b => b.Showtime)
                .Where(b => b.UserId == userId && (
                    b.Status == BookingStatus.Paid
                    || b.Status == BookingStatus.Refunded
                    || (b.Status == BookingStatus.Pending && b.ExpiresAtUtc > now)))
                .OrderBy(b => b.Showtime!.StartsAtUtc)
                .ToListAsync(ct);
            return list.Select(BookingDto.From).ToList();
        })
            .WithName("GetMyBookings");

        mine.MapGet("/{bookingId:guid}", async (
                Guid bookingId,
                ClaimsPrincipal user,
                AppDbContext db,
                BookingService bookings,
                CancellationToken ct) =>
        {
            var booking = await FindOwnAsync(db, user, bookingId, ct);
            if (booking is null)
            {
                return Results.NotFound();
            }

            await bookings.ReconcileAsync(booking, ct);
            return Results.Ok(BookingDto.From(booking));
        })
            .WithName("GetMyBooking");

        mine.MapPost("/{bookingId:guid}/cancel", async (
                Guid bookingId,
                ClaimsPrincipal user,
                AppDbContext db,
                BookingService bookings,
                IPaymentGateway payments,
                CancellationToken ct) =>
        {
            var booking = await FindOwnAsync(db, user, bookingId, ct);
            if (booking is null)
            {
                return Results.NotFound();
            }

            // the user may have paid a moment ago; don't release a paid booking as if it were a hold
            await bookings.ReconcileAsync(booking, ct);

            switch (booking.Status)
            {
                case BookingStatus.Pending:
                    if (booking.StripeSessionId is not null)
                    {
                        await bookings.TryExpireCheckoutAsync(booking.StripeSessionId, ct);
                    }
                    await bookings.ReleaseAsync(booking, BookingStatus.Cancelled, ct);
                    break;

                case BookingStatus.Paid when BookingDto.From(booking).CanCancel:
                    await payments.RefundAsync(booking.StripePaymentIntentId!, ct);
                    await bookings.ReleaseAsync(booking, BookingStatus.Refunded, ct);
                    break;

                case BookingStatus.Paid:
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Too late to cancel",
                        detail: "Tickets can be refunded until an hour before the showtime.");
            }

            return Results.Ok(BookingDto.From(booking));
        })
            .WithName("CancelMyBooking");

        // Stripe calls this; authenticity comes from the signature, not a session
        app.MapPost("/api/payments/stripe-webhook", async (
                HttpContext http,
                IPaymentGateway payments,
                AppDbContext db,
                BookingService bookings,
                ILogger<BookingService> logger,
                CancellationToken ct) =>
        {
            using var reader = new StreamReader(http.Request.Body);
            var payload = await reader.ReadToEndAsync(ct);

            PaymentWebhook webhook;
            try
            {
                webhook = payments.ParseWebhook(payload, http.Request.Headers["Stripe-Signature"].ToString());
            }
            catch (PaymentWebhookException ex)
            {
                logger.LogWarning(ex, "Rejected a payment webhook");
                return Results.BadRequest();
            }

            if (webhook.Checkout is { } checkout)
            {
                switch (webhook.Type)
                {
                    case "checkout.session.completed" or "checkout.session.async_payment_succeeded" when checkout.Paid:
                        await bookings.MarkPaidAsync(checkout, ct);
                        break;

                    case "checkout.session.expired":
                        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.StripeSessionId == checkout.SessionId, ct);
                        if (booking is { Status: BookingStatus.Pending })
                        {
                            await bookings.ReleaseAsync(booking, BookingStatus.Expired, ct);
                        }
                        break;
                }
            }

            return Results.Ok();
        })
            .DisableRateLimiting()
            .WithTags("Tickets")
            .WithName("StripeWebhook");

        return app;
    }

    private static Task<Booking?> FindOwnAsync(AppDbContext db, ClaimsPrincipal user, Guid bookingId, CancellationToken ct)
    {
        var userId = user.GetUserId();
        return db.Bookings.Include(b => b.Showtime).SingleOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId, ct);
    }

    private static string SeatTypeName(SeatType type) => type switch
    {
        SeatType.Vip => "VIP seat",
        SeatType.Wheelchair => "Wheelchair space",
        _ => "Standard seat",
    };
}
