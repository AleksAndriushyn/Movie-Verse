using MovieVerse.Api.Data;

namespace MovieVerse.Api.Tickets;

public sealed class TicketsOptions
{
    public const string SectionName = "Tickets";

    /// <summary>Make the demo halls look lived-in by treating some seats as sold.</summary>
    public bool SimulateOccupancy { get; set; } = true;
}

/// <summary>
/// Pretend audiences for the demo cinemas. Every (showtime, seat) pair gets a fixed pseudo-random draw; a seat
/// counts as sold once the showtime's fill level, weighted by how desirable the seat is, rises above it.
/// Fill only grows as the showtime approaches, so a seat that looks sold stays sold, and nothing is stored.
/// </summary>
public static class SimulatedOccupancy
{
    /// <summary>Never sell out completely, so every showtime stays bookable.</summary>
    private const double MaxSeatProbability = 0.97;

    private static readonly IReadOnlyDictionary<int, double> WeightBySeat = BuildWeights();

    public static bool IsTaken(Showtime showtime, int seatId, DateTime nowUtc)
    {
        if (!WeightBySeat.TryGetValue(seatId, out var weight))
        {
            return false;
        }

        var probability = Math.Min(MaxSeatProbability, FillLevel(showtime, nowUtc) * weight);
        return Draw(showtime.Id, seatId) < probability;
    }

    /// <summary>
    /// Share of an average seat that's sold: higher for evening and weekend shows, rising from ~15% of that
    /// a week ahead to all of it at showtime.
    /// </summary>
    public static double FillLevel(Showtime showtime, DateTime nowUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(showtime.StartsAtUtc, ShowtimeScheduler.Zone);
        var byTimeOfDay = local.Hour switch
        {
            < 12 => 0.2,
            < 15 => 0.3,
            < 18 => 0.4,
            < 21 => 0.7,
            _ => 0.5,
        };
        var weekend = local.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday ? 1.2 : 1.0;

        var daysAhead = Math.Max(0, (showtime.StartsAtUtc - nowUtc).TotalDays);
        var proximity = Math.Clamp(1 - daysAhead / ShowtimeScheduler.DaysAhead, 0.15, 1);

        return byTimeOfDay * weekend * proximity;
    }

    // SplitMix64: a stable hash (unlike string.GetHashCode) mapped to [0, 1)
    private static double Draw(int showtimeId, int seatId)
    {
        var x = unchecked(((ulong)(uint)showtimeId << 32) | (uint)seatId) + 0x9E3779B97F4A7C15UL;
        x = unchecked((x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL);
        x = unchecked((x ^ (x >> 27)) * 0x94D049BB133111EBUL);
        x ^= x >> 31;
        return (x >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// How likely each seat is to be bought, relative to the hall's average (which is 1): centre seats and the
    /// middle-to-back rows first, the front row and the edges last, wheelchair spaces rarely.
    /// </summary>
    private static Dictionary<int, double> BuildWeights()
    {
        var weights = new Dictionary<int, double>();
        foreach (var hall in CinemaSeed.Seats.GroupBy(s => s.HallId))
        {
            var rows = hall.GroupBy(s => s.Row).OrderBy(r => r.Key).ToList();
            var raw = new Dictionary<int, double>();
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var centre = (row.Min(s => s.Column) + row.Max(s => s.Column)) / 2.0;
                var halfWidth = Math.Max(1, (row.Max(s => s.Column) - row.Min(s => s.Column)) / 2.0);
                var depth = rows.Count > 1 ? rowIndex / (double)(rows.Count - 1) : 0.5;

                foreach (var seat in row)
                {
                    var horizontal = 1 - 0.5 * Math.Abs(seat.Column - centre) / halfWidth;
                    var vertical = 1 - 0.6 * Math.Abs(depth - 0.6);
                    var type = seat.Type == SeatType.Wheelchair ? 0.3 : 1.0;
                    raw[seat.Id] = horizontal * vertical * type;
                }
            }

            var average = raw.Values.Average();
            foreach (var (seatId, weight) in raw)
            {
                weights[seatId] = weight / average;
            }
        }

        return weights;
    }
}
