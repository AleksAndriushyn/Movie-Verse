using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MovieVerse.Api.Auth;
using MovieVerse.Api.Data;

namespace MovieVerse.Api.Tickets;

public sealed record CinemaDto(int Id, string Name, string Address, string TimeZone)
{
    public static CinemaDto From(Cinema cinema) => new(cinema.Id, cinema.Name, cinema.Address, cinema.TimeZone);
}

public sealed record ShowtimeSlot(int Id, int CinemaId, string HallName, DateTime StartsAt, bool Bookable);

public sealed record MovieSchedule(int MovieId, string Title, string? PosterPath, IReadOnlyList<ShowtimeSlot> Showtimes);

public sealed record DaySchedule(
    DateOnly Date,
    IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<CinemaDto> Cinemas,
    IReadOnlyList<MovieSchedule> Movies);

public sealed record MovieShowtime(int Id, int CinemaId, string HallName, DateOnly Date, DateTime StartsAt, bool Bookable);

public sealed record MovieShowtimes(IReadOnlyList<CinemaDto> Cinemas, IReadOnlyList<MovieShowtime> Showtimes);

public enum SeatStatus
{
    Free,
    Taken,

    /// <summary>Held by the viewer's own pending booking.</summary>
    Mine,
}

public sealed record SeatDto(int Id, int Number, int Column, SeatType Type, SeatStatus Status);

public sealed record SeatRow(string Row, IReadOnlyList<SeatDto> Seats);

public sealed record ShowtimeDetails(
    int Id,
    int MovieId,
    string MovieTitle,
    string? PosterPath,
    DateTime StartsAt,
    CinemaDto Cinema,
    string HallName,
    int StandardPriceCents,
    int VipPriceCents,
    string Currency,
    bool Bookable,
    IReadOnlyList<SeatRow> Rows,
    Guid? MyHoldBookingId,
    DateTime? MyHoldExpiresAt);

public static class ShowtimeEndpoints
{
    private static readonly IReadOnlyDictionary<int, Hall> HallsById = CinemaSeed.Halls.ToDictionary(h => h.Id);

    public static IReadOnlyList<CinemaDto> Cinemas { get; } = CinemaSeed.Cinemas.Select(CinemaDto.From).ToList();

    public static CinemaDto CinemaOfHall(int hallId) => Cinemas.Single(c => c.Id == HallsById[hallId].CinemaId);

    public static string HallName(int hallId) => HallsById[hallId].Name;

    public static IEndpointRouteBuilder MapShowtimeEndpoints(this IEndpointRouteBuilder app)
    {
        var showtimes = app.MapGroup("/api/showtimes").WithTags("Tickets");

        // Everything playing on one day, grouped by movie
        showtimes.MapGet("/", async (
                ShowtimeScheduler scheduler,
                AppDbContext db,
                DateOnly? date,
                CancellationToken ct) =>
        {
            await scheduler.EnsureScheduleAsync(ct);
            var day = date ?? ShowtimeScheduler.Today;

            var rows = await db.Showtimes
                .Where(s => s.LocalDate == day)
                .OrderBy(s => s.StartsAtUtc)
                .ToListAsync(ct);

            var movies = rows
                .GroupBy(s => new { s.MovieId, s.MovieTitle, s.PosterPath })
                .OrderBy(g => g.Key.MovieTitle)
                .Select(g => new MovieSchedule(
                    g.Key.MovieId,
                    g.Key.MovieTitle,
                    g.Key.PosterPath,
                    g.Select(s => new ShowtimeSlot(
                        s.Id, CinemaOfHall(s.HallId).Id, HallName(s.HallId), s.StartsAtUtc, BookingService.IsBookable(s))).ToList()))
                .ToList();

            return new DaySchedule(day, ShowtimeScheduler.UpcomingDates, Cinemas, movies);
        })
            .WithName("GetSchedule");

        app.MapGet("/api/movies/{movieId:int}/showtimes", async (
                [Range(1, int.MaxValue)] int movieId,
                ShowtimeScheduler scheduler,
                AppDbContext db,
                CancellationToken ct) =>
        {
            await scheduler.EnsureScheduleAsync(ct);
            var now = DateTime.UtcNow;

            var rows = await db.Showtimes
                .Where(s => s.MovieId == movieId && s.StartsAtUtc > now)
                .OrderBy(s => s.StartsAtUtc)
                .ToListAsync(ct);

            return new MovieShowtimes(
                Cinemas,
                rows.Select(s => new MovieShowtime(
                    s.Id, CinemaOfHall(s.HallId).Id, HallName(s.HallId), s.LocalDate, s.StartsAtUtc, BookingService.IsBookable(s))).ToList());
        })
            .WithTags("Tickets")
            .WithName("GetMovieShowtimes");

        // The seat map; anyone can look, a signed-in viewer also sees their own hold
        showtimes.MapGet("/{showtimeId:int}", async (
                int showtimeId,
                ClaimsPrincipal user,
                AppDbContext db,
                BookingService bookings,
                CancellationToken ct) =>
        {
            var showtime = await db.Showtimes.SingleOrDefaultAsync(s => s.Id == showtimeId, ct);
            if (showtime is null)
            {
                return Results.NotFound();
            }

            await bookings.ReleaseExpiredHoldsAsync(showtimeId, ct);
            int? viewerId = user.Identity?.IsAuthenticated == true ? user.GetUserId() : null;

            var taken = await (
                from bookingSeat in db.BookingSeats
                join booking in db.Bookings on bookingSeat.BookingId equals booking.Id
                where bookingSeat.ShowtimeId == showtimeId
                select new { bookingSeat.SeatId, booking.Id, booking.UserId, booking.Status, booking.ExpiresAtUtc })
                .ToListAsync(ct);

            var mine = taken.FirstOrDefault(t => t.UserId == viewerId && t.Status == BookingStatus.Pending);
            var statusBySeat = taken.ToDictionary(
                t => t.SeatId,
                t => t.UserId == viewerId && t.Status == BookingStatus.Pending ? SeatStatus.Mine : SeatStatus.Taken);

            var seats = await db.Seats
                .Where(s => s.HallId == showtime.HallId)
                .OrderBy(s => s.Row)
                .ThenBy(s => s.Column)
                .ToListAsync(ct);

            var rows = seats
                .GroupBy(s => s.Row)
                .Select(row => new SeatRow(row.Key, row.Select(seat => new SeatDto(
                    seat.Id,
                    seat.Number,
                    seat.Column,
                    seat.Type,
                    statusBySeat.GetValueOrDefault(seat.Id, SeatStatus.Free))).ToList()))
                .ToList();

            return Results.Ok(new ShowtimeDetails(
                showtime.Id,
                showtime.MovieId,
                showtime.MovieTitle,
                showtime.PosterPath,
                showtime.StartsAtUtc,
                CinemaOfHall(showtime.HallId),
                HallName(showtime.HallId),
                showtime.StandardPriceCents,
                showtime.VipPriceCents,
                Booking.Currency,
                BookingService.IsBookable(showtime),
                rows,
                mine?.Id,
                mine?.ExpiresAtUtc));
        })
            .WithName("GetShowtime");

        return app;
    }
}
