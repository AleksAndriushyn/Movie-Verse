namespace MovieVerse.Api.Data;

public sealed class Showtime
{
    public int Id { get; set; }

    public int HallId { get; set; }

    public Hall? Hall { get; set; }

    public int MovieId { get; set; }

    /// <summary>Movie card data at scheduling time, so schedule pages need no TMDB calls.</summary>
    public required string MovieTitle { get; set; }

    public string? PosterPath { get; set; }

    public DateTime StartsAtUtc { get; set; }

    /// <summary>The cinema-local date the showtime belongs to; one schedule is generated per date.</summary>
    public DateOnly LocalDate { get; set; }

    public int StandardPriceCents { get; set; }

    public int VipPriceCents { get; set; }
}
