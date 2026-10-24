namespace MovieVerse.Api.Data;

public enum BookingStatus : byte
{
    /// <summary>Seats are held while the user pays.</summary>
    Pending = 0,
    Paid = 1,
    Cancelled = 2,

    /// <summary>The hold ran out before payment; the seats were released.</summary>
    Expired = 3,
    Refunded = 4,
}

public sealed class Booking
{
    public const string Currency = "usd";

    public Guid Id { get; set; }

    public int UserId { get; set; }

    public int ShowtimeId { get; set; }

    public Showtime? Showtime { get; set; }

    public BookingStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When a pending hold stops reserving its seats.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public int TotalCents { get; set; }

    /// <summary>Seat labels ("F7, F8") kept after the seats are released, for history.</summary>
    public required string SeatLabels { get; set; }

    public string? StripeSessionId { get; set; }

    /// <summary>Stripe's hosted payment page for a pending booking, so the user can resume paying.</summary>
    public string? CheckoutUrl { get; set; }

    public string? StripePaymentIntentId { get; set; }

    /// <summary>Short code printed on the ticket and in its QR code; set once paid.</summary>
    public string? TicketCode { get; set; }

    public List<BookingSeat> Seats { get; set; } = [];
}

/// <summary>
/// A seat taken by a pending or paid booking. Unique per showtime and seat, so the database
/// itself refuses to sell a seat twice; rows are deleted when a booking expires or is cancelled.
/// </summary>
public sealed class BookingSeat
{
    public Guid BookingId { get; set; }

    public int ShowtimeId { get; set; }

    public int SeatId { get; set; }

    public Seat? Seat { get; set; }

    public int PriceCents { get; set; }
}
