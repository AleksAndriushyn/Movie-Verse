using Microsoft.EntityFrameworkCore;
using MovieVerse.Api.Data;
using MovieVerse.Api.Movies;
using MovieVerse.Api.Tmdb;

namespace MovieVerse.Api.Tickets;

/// <summary>
/// Plans the demo cinemas' schedule: every hall shows a rotation of the movies now playing in the US,
/// for the next <see cref="DaysAhead"/> days. Days are generated on demand, once each.
/// </summary>
public sealed class ShowtimeScheduler(AppDbContext db, MovieCatalog catalog, ILogger<ShowtimeScheduler> logger)
{
    public const int DaysAhead = 7;
    public const int MoviesInRotation = 8;
    public const int StandardPriceCents = 1200;
    public const int VipPriceCents = 1800;

    private static readonly TimeOnly[] Slots = [new(10, 30), new(13, 30), new(16, 30), new(19, 30), new(22, 15)];

    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(CinemaSeed.TimeZone);

    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone));

    public static IReadOnlyList<DateOnly> UpcomingDates =>
        Enumerable.Range(0, DaysAhead).Select(day => Today.AddDays(day)).ToList();

    public async Task EnsureScheduleAsync(CancellationToken ct)
    {
        var dates = UpcomingDates;
        var (first, last) = (dates[0], dates[^1]);
        var scheduled = await db.Showtimes
            .Where(s => s.LocalDate >= first && s.LocalDate <= last)
            .Select(s => s.LocalDate)
            .Distinct()
            .ToListAsync(ct);
        var missing = dates.Except(scheduled).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        IReadOnlyList<MovieSummary> movies;
        try
        {
            movies = (await catalog.GetNowPlayingAsync(ct)).Take(MoviesInRotation).ToList();
        }
        catch (Exception ex) when (ex is TmdbException or HttpRequestException)
        {
            // keep serving what's already scheduled; the next request tries again
            logger.LogWarning(ex, "Couldn't load now-playing movies to schedule {Days} days", missing.Count);
            return;
        }

        if (movies.Count == 0)
        {
            return;
        }

        foreach (var date in missing)
        {
            db.Showtimes.AddRange(BuildDay(date, movies));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // a concurrent request scheduled this day first (unique hall + start time)
                db.ChangeTracker.Clear();
            }
        }
    }

    private static IEnumerable<Showtime> BuildDay(DateOnly date, IReadOnlyList<MovieSummary> movies)
    {
        foreach (var hall in CinemaSeed.Halls)
        {
            for (var slot = 0; slot < Slots.Length; slot++)
            {
                // a stable rotation: same day, hall and slot always get the same movie
                var movie = movies[(date.DayNumber * 3 + hall.Id * 5 + slot) % movies.Count];
                yield return new Showtime
                {
                    HallId = hall.Id,
                    MovieId = movie.Id,
                    MovieTitle = movie.Title,
                    PosterPath = movie.PosterPath,
                    LocalDate = date,
                    StartsAtUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(Slots[slot]), Zone),
                    StandardPriceCents = StandardPriceCents,
                    VipPriceCents = VipPriceCents,
                };
            }
        }
    }
}
