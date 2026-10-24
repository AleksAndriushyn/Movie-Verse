using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieVerse.Api.Data;
using MovieVerse.Api.Tickets;

namespace MovieVerse.Api.Tests;

public sealed class SimulatedOccupancyTests
{
    private static readonly Seat[] KyivSeats = CinemaSeed.Seats.Where(s => s.HallId == 1).ToArray();

    /// <summary>A Kyiv-hall showtime at a San Diego local time.</summary>
    private static Showtime ShowtimeAt(int id, DateTime local) => new()
    {
        Id = id,
        HallId = 1,
        MovieTitle = "Test",
        StartsAtUtc = TimeZoneInfo.ConvertTimeToUtc(local, ShowtimeScheduler.Zone),
    };

    private static double Occupancy(Showtime showtime, DateTime nowUtc) =>
        KyivSeats.Count(seat => SimulatedOccupancy.IsTaken(showtime, seat.Id, nowUtc)) / (double)KyivSeats.Length;

    // A Wednesday, so no weekend boost
    private static readonly DateTime Wednesday = new(2026, 10, 28);

    [Fact]
    public void SameShowtimeAndTime_GiveTheSameSeats()
    {
        var showtime = ShowtimeAt(42, Wednesday.AddHours(19.5));
        var now = showtime.StartsAtUtc.AddHours(-3);

        var first = KyivSeats.Where(s => SimulatedOccupancy.IsTaken(showtime, s.Id, now)).Select(s => s.Id).ToList();
        var second = KyivSeats.Where(s => SimulatedOccupancy.IsTaken(showtime, s.Id, now)).Select(s => s.Id).ToList();

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void SeatsTakenEarlier_StayTakenAsTheShowApproaches()
    {
        var showtime = ShowtimeAt(7, Wednesday.AddHours(19.5));
        var earlier = KyivSeats.Where(s => SimulatedOccupancy.IsTaken(showtime, s.Id, showtime.StartsAtUtc.AddDays(-4))).ToList();
        var later = KyivSeats.Where(s => SimulatedOccupancy.IsTaken(showtime, s.Id, showtime.StartsAtUtc.AddHours(-1))).ToList();

        Assert.True(later.Count > earlier.Count);
        Assert.All(earlier, seat => Assert.Contains(seat, later));
    }

    [Fact]
    public void EveningShows_FillUpMoreThanMornings_AndNeverSellOut()
    {
        double Average(double hour) => Enumerable.Range(1, 40)
            .Select(id => ShowtimeAt(id, Wednesday.AddHours(hour)))
            .Average(s => Occupancy(s, s.StartsAtUtc.AddMinutes(-30)));

        var morning = Average(10.5);
        var evening = Average(19.5);

        Assert.InRange(morning, 0.1, 0.3);
        Assert.InRange(evening, 0.55, 0.85);
        Assert.All(Enumerable.Range(1, 40), id =>
        {
            var friday = ShowtimeAt(id, Wednesday.AddDays(2).AddHours(19.5));
            Assert.True(Occupancy(friday, friday.StartsAtUtc) < 1);
        });
    }

    [Fact]
    public void AWeekAhead_TheHallIsAlmostEmpty()
    {
        var showtime = ShowtimeAt(3, Wednesday.AddHours(19.5));

        Assert.InRange(Occupancy(showtime, showtime.StartsAtUtc.AddDays(-7)), 0, 0.2);
    }

    [Fact]
    public void CentreSeats_SellMoreOftenThanEdgesAndWheelchairSpaces()
    {
        // row E: "ssss.ssssssssss.ssss"; seat 9 sits mid-hall, seat 1 on the left edge
        var centre = KyivSeats.Single(s => s.Row == "E" && s.Number == 9).Id;
        var edge = KyivSeats.Single(s => s.Row == "E" && s.Number == 1).Id;
        var wheelchair = KyivSeats.First(s => s.Type == SeatType.Wheelchair).Id;

        int TakenCount(int seatId) => Enumerable.Range(1, 400)
            .Select(id => ShowtimeAt(id, Wednesday.AddHours(19.5)))
            .Count(s => SimulatedOccupancy.IsTaken(s, seatId, s.StartsAtUtc));

        Assert.True(TakenCount(centre) > TakenCount(edge));
        Assert.True(TakenCount(edge) > TakenCount(wheelchair));
    }
}

public sealed class SimulatedOccupancyApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public SimulatedOccupancyApiTests()
    {
        _factory.Settings["Tickets:SimulateOccupancy"] = "true";
        _factory.Tmdb.Respond("movie/now_playing?region=US&page=1",
            """{"page":1,"results":[{"id":101,"title":"Alpha","poster_path":"/a.jpg","vote_average":7}]}""");
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SimulatedSeats_LookTakenAndCannotBeBooked()
    {
        await _factory.CreateClient().GetAsync("/api/showtimes");
        int showtimeId;
        using (var scope = _factory.Services.CreateScope())
        {
            // the soonest evening show in the big hall: the fullest one in the schedule
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var soon = DateTime.UtcNow.AddHours(1);
            var evening = (await db.Showtimes.Where(s => s.HallId == 1 && s.StartsAtUtc > soon).ToListAsync())
                .Where(s => TimeZoneInfo.ConvertTimeFromUtc(s.StartsAtUtc, ShowtimeScheduler.Zone).Hour == 19)
                .OrderBy(s => s.StartsAtUtc)
                .First();
            showtimeId = evening.Id;
        }

        var client = _factory.CreateClientFor(await _factory.AddUserAsync("google-alice"));
        var details = await client.GetFromJsonAsync<JsonElement>($"/api/showtimes/{showtimeId}");
        var seats = details.GetProperty("rows").EnumerateArray().SelectMany(r => r.GetProperty("seats").EnumerateArray()).ToList();
        var taken = seats.First(s => s.GetProperty("status").GetString() == "taken").GetProperty("id").GetInt32();
        var free = seats.First(s => s.GetProperty("status").GetString() == "free").GetProperty("id").GetInt32();

        var bookTaken = await client.PostAsJsonAsync($"/api/showtimes/{showtimeId}/bookings", new { seatIds = new[] { taken } });
        var bookFree = await client.PostAsJsonAsync($"/api/showtimes/{showtimeId}/bookings", new { seatIds = new[] { free } });

        Assert.Equal(HttpStatusCode.Conflict, bookTaken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bookFree.StatusCode);
        Assert.Single(_factory.Payments.Sessions);
    }
}
