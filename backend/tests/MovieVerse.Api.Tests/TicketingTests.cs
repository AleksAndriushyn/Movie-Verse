using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieVerse.Api.Data;
using MovieVerse.Api.Tickets;

namespace MovieVerse.Api.Tests;

public sealed class TicketingTests : IDisposable
{
    private const string NowPlayingJson = """
        {"page":1,"results":[
          {"id":101,"title":"Alpha","poster_path":"/alpha.jpg","release_date":"2026-10-01","vote_average":7.1,"overview":"A"},
          {"id":202,"title":"Bravo","poster_path":"/bravo.jpg","release_date":"2026-10-02","vote_average":6.4,"overview":"B"},
          {"id":303,"title":"No Poster","poster_path":null,"release_date":"2026-10-03","vote_average":5.0,"overview":"C"}
        ]}
        """;

    /// <summary>Kyiv hall seats are hall id 1 * 1000 + position; row A starts with two wheelchair spaces.</summary>
    private const int KyivA1 = 1001, KyivA2 = 1002, KyivA3 = 1003, LvivA1 = 2001;

    private readonly ApiFactory _factory = new();

    public TicketingTests() =>
        _factory.Tmdb.Respond("movie/now_playing?region=US&page=1", NowPlayingJson);

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> UserAsync(string subject) =>
        _factory.CreateClientFor(await _factory.AddUserAsync(subject));

    /// <summary>The first bookable showtime in the Kyiv hall (hall 1).</summary>
    private async Task<int> KyivShowtimeAsync()
    {
        await _factory.CreateClient().GetAsync("/api/showtimes");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var soon = DateTime.UtcNow.AddHours(2);
        return await db.Showtimes.Where(s => s.HallId == 1 && s.StartsAtUtc > soon).OrderBy(s => s.StartsAtUtc).Select(s => s.Id).FirstAsync();
    }

    private static Task<HttpResponseMessage> Book(HttpClient client, int showtimeId, params int[] seatIds) =>
        client.PostAsJsonAsync($"/api/showtimes/{showtimeId}/bookings", new { seatIds });

    private static async Task<(Guid BookingId, string SessionId)> BookAsync(HttpClient client, int showtimeId, params int[] seatIds)
    {
        var response = await Book(client, showtimeId, seatIds);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = body.GetProperty("checkoutUrl").GetString()!;
        return (body.GetProperty("bookingId").GetGuid(), url[(url.LastIndexOf('/') + 1)..]);
    }

    private static async Task<Dictionary<int, string>> SeatStatusesAsync(HttpClient client, int showtimeId)
    {
        var details = await client.GetFromJsonAsync<JsonElement>($"/api/showtimes/{showtimeId}");
        return details.GetProperty("rows").EnumerateArray()
            .SelectMany(row => row.GetProperty("seats").EnumerateArray())
            .ToDictionary(seat => seat.GetProperty("id").GetInt32(), seat => seat.GetProperty("status").GetString()!);
    }

    private Task<HttpResponseMessage> Webhook(string type, string sessionId, string signature = FakePaymentGateway.ValidSignature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe-webhook")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { type, sessionId }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signature);
        return _factory.CreateClient().SendAsync(request);
    }

    private async Task UpdateAsync(Func<AppDbContext, Task> change)
    {
        using var scope = _factory.Services.CreateScope();
        await change(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task Schedule_CoversAWeekOfNowPlayingMoviesInEveryHall()
    {
        var schedule = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/showtimes");

        Assert.Equal(ShowtimeScheduler.DaysAhead, schedule.GetProperty("dates").GetArrayLength());
        Assert.Equal(2, schedule.GetProperty("cinemas").GetArrayLength());
        var movies = schedule.GetProperty("movies").EnumerateArray().Select(m => m.GetProperty("title").GetString()).ToList();
        Assert.Equal(["Alpha", "Bravo"], movies);

        await UpdateAsync(async db =>
        {
            // 4 halls x 5 slots, for each of the 7 days, generated once
            Assert.Equal(4 * 5 * ShowtimeScheduler.DaysAhead, await db.Showtimes.CountAsync());
        });
        await _factory.CreateClient().GetAsync("/api/showtimes");
        await UpdateAsync(async db => Assert.Equal(4 * 5 * ShowtimeScheduler.DaysAhead, await db.Showtimes.CountAsync()));
    }

    [Fact]
    public async Task Schedule_WithTmdbDown_IsEmptyNotAnError()
    {
        _factory.Tmdb.Respond("movie/now_playing?region=US&page=1", "{}", HttpStatusCode.InternalServerError);

        var response = await _factory.CreateClient().GetAsync("/api/showtimes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("movies").GetArrayLength());
    }

    [Fact]
    public async Task MovieShowtimes_ListOnlyThatMoviesUpcomingShows()
    {
        var showtimes = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/movies/101/showtimes");
        var other = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/movies/999/showtimes");

        Assert.NotEqual(0, showtimes.GetProperty("showtimes").GetArrayLength());
        Assert.All(showtimes.GetProperty("showtimes").EnumerateArray(),
            s => Assert.True(s.GetProperty("startsAt").GetDateTime() > DateTime.UtcNow));
        Assert.Equal(0, other.GetProperty("showtimes").GetArrayLength());
    }

    [Fact]
    public async Task SeatMap_FollowsTheHallLayout()
    {
        var showtimeId = await KyivShowtimeAsync();

        var details = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/showtimes/{showtimeId}");

        Assert.Equal("Kyiv", details.GetProperty("hallName").GetString());
        Assert.Equal("Flamingo Screens", details.GetProperty("cinema").GetProperty("name").GetString());
        var rows = details.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(10, rows.Count);
        var rowA = rows[0].GetProperty("seats").EnumerateArray().ToList();
        Assert.Equal("wheelchair", rowA[0].GetProperty("type").GetString());
        Assert.Equal(0, rowA[0].GetProperty("column").GetInt32());
        Assert.Equal(5, rowA[2].GetProperty("column").GetInt32()); // after the "..." gap
        Assert.Equal("vip", rows[^1].GetProperty("seats")[0].GetProperty("type").GetString());
        Assert.All(rows.SelectMany(r => r.GetProperty("seats").EnumerateArray()), s => Assert.Equal("free", s.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Booking_HoldsSeatsAndOpensCheckout()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");

        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA3, KyivA1);

        var checkout = _factory.Payments.Sessions[sessionId];
        Assert.Equal(bookingId, checkout.BookingId);
        Assert.Equal(2 * ShowtimeScheduler.StandardPriceCents, checkout.Lines.Sum(l => l.UnitAmountCents * l.Quantity));
        Assert.StartsWith($"http://localhost/tickets/{bookingId}", checkout.SuccessUrl);
        Assert.InRange(checkout.ExpiresAtUtc - DateTime.UtcNow, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(32));

        var asAlice = await SeatStatusesAsync(alice, showtimeId);
        Assert.Equal("mine", asAlice[KyivA1]);
        Assert.Equal("free", asAlice[KyivA2]);
        var asVisitor = await SeatStatusesAsync(_factory.CreateClient(), showtimeId);
        Assert.Equal("taken", asVisitor[KyivA3]);

        var booking = await alice.GetFromJsonAsync<JsonElement>($"/api/me/bookings/{bookingId}");
        Assert.Equal("pending", booking.GetProperty("status").GetString());
        Assert.Equal("A1, A3", booking.GetProperty("seatLabels").GetString());
    }

    [Fact]
    public async Task SameSeat_CannotBeHeldTwice()
    {
        var showtimeId = await KyivShowtimeAsync();
        await BookAsync(await UserAsync("google-alice"), showtimeId, KyivA1);

        var second = await Book(await UserAsync("google-bob"), showtimeId, KyivA1, KyivA2);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        // the failed attempt mustn't leave A2 held
        Assert.Equal("free", (await SeatStatusesAsync(_factory.CreateClient(), showtimeId))[KyivA2]);
    }

    [Fact]
    public async Task ExpiredHold_FreesItsSeats()
    {
        var showtimeId = await KyivShowtimeAsync();
        var (bookingId, _) = await BookAsync(await UserAsync("google-alice"), showtimeId, KyivA1);
        await UpdateAsync(db => db.Bookings.Where(b => b.Id == bookingId)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1))));

        var bob = await UserAsync("google-bob");
        await BookAsync(bob, showtimeId, KyivA1);

        await UpdateAsync(async db =>
            Assert.Equal(BookingStatus.Expired, (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status));
    }

    [Fact]
    public async Task NewSelection_ReplacesTheUsersEarlierHold()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (_, firstSession) = await BookAsync(alice, showtimeId, KyivA1);

        await BookAsync(alice, showtimeId, KyivA2);

        var seats = await SeatStatusesAsync(alice, showtimeId);
        Assert.Equal("free", seats[KyivA1]);
        Assert.Equal("mine", seats[KyivA2]);
        Assert.Contains(firstSession, _factory.Payments.Expired);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { LvivA1 })]
    [InlineData(new[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 1011 })]
    public async Task InvalidSeatSelection_Returns400(int[] seatIds)
    {
        var showtimeId = await KyivShowtimeAsync();

        var response = await Book(await UserAsync("google-alice"), showtimeId, seatIds);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task Booking_WithoutSession_Returns401()
    {
        var showtimeId = await KyivShowtimeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Book(_factory.CreateClient(), showtimeId, KyivA1)).StatusCode);
    }

    [Fact]
    public async Task Booking_CloseToStart_IsRefused()
    {
        var showtimeId = await KyivShowtimeAsync();
        await UpdateAsync(db => db.Showtimes.Where(s => s.Id == showtimeId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.StartsAtUtc, DateTime.UtcNow.AddMinutes(10))));

        var response = await Book(await UserAsync("google-alice"), showtimeId, KyivA1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CheckoutFailure_ReleasesTheSeats()
    {
        var showtimeId = await KyivShowtimeAsync();
        _factory.Payments.FailNextCheckout = true;

        var response = await Book(await UserAsync("google-alice"), showtimeId, KyivA1);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("free", (await SeatStatusesAsync(_factory.CreateClient(), showtimeId))[KyivA1]);
    }

    [Fact]
    public async Task PaidWebhook_IssuesTicketOnce()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA1);
        _factory.Payments.Pay(sessionId);

        Assert.Equal(HttpStatusCode.OK, (await Webhook("checkout.session.completed", sessionId)).StatusCode);
        var first = await alice.GetFromJsonAsync<JsonElement>($"/api/me/bookings/{bookingId}");
        await Webhook("checkout.session.completed", sessionId);
        var second = await alice.GetFromJsonAsync<JsonElement>($"/api/me/bookings/{bookingId}");

        Assert.Equal("paid", first.GetProperty("status").GetString());
        Assert.Matches("^[A-Z2-9]{8}$", first.GetProperty("ticketCode").GetString());
        Assert.Equal(first.GetProperty("ticketCode").GetString(), second.GetProperty("ticketCode").GetString());
        Assert.Equal("taken", (await SeatStatusesAsync(_factory.CreateClient(), showtimeId))[KyivA1]);
        var tickets = await alice.GetFromJsonAsync<JsonElement>("/api/me/bookings");
        Assert.Equal(bookingId, Assert.Single(tickets.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Webhook_WithBadSignature_IsRejected()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA1);
        _factory.Payments.Pay(sessionId);

        var response = await Webhook("checkout.session.completed", sessionId, signature: "forged");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await UpdateAsync(async db =>
            Assert.Equal(BookingStatus.Pending, (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status));
    }

    [Fact]
    public async Task ExpiredWebhook_ReleasesTheHold()
    {
        var showtimeId = await KyivShowtimeAsync();
        var (_, sessionId) = await BookAsync(await UserAsync("google-alice"), showtimeId, KyivA1);

        await Webhook("checkout.session.expired", sessionId);

        Assert.Equal("free", (await SeatStatusesAsync(_factory.CreateClient(), showtimeId))[KyivA1]);
    }

    [Fact]
    public async Task ReturningFromCheckout_ConfirmsPaymentWithoutWebhook()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA1);
        _factory.Payments.Pay(sessionId);

        var booking = await alice.GetFromJsonAsync<JsonElement>($"/api/me/bookings/{bookingId}");

        Assert.Equal("paid", booking.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CancellingAHold_FreesSeatsAndClosesCheckout()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA1);

        var cancel = await alice.PostAsync($"/api/me/bookings/{bookingId}/cancel", null);

        Assert.Equal("cancelled", (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Contains(sessionId, _factory.Payments.Expired);
        Assert.Equal("free", (await SeatStatusesAsync(alice, showtimeId))[KyivA1]);
    }

    [Fact]
    public async Task CancellingAPaidTicket_RefundsAndFreesTheSeat()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA1);
        _factory.Payments.Pay(sessionId);
        await Webhook("checkout.session.completed", sessionId);

        var cancel = await alice.PostAsync($"/api/me/bookings/{bookingId}/cancel", null);

        Assert.Equal("refunded", (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Contains($"pi_{sessionId}", _factory.Payments.Refunded);
        Assert.Equal("free", (await SeatStatusesAsync(alice, showtimeId))[KyivA1]);
    }

    [Fact]
    public async Task PaidTicket_CannotBeRefundedInTheLastHour()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA1);
        _factory.Payments.Pay(sessionId);
        await Webhook("checkout.session.completed", sessionId);
        await UpdateAsync(db => db.Showtimes.Where(s => s.Id == showtimeId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.StartsAtUtc, DateTime.UtcNow.AddMinutes(30))));

        var cancel = await alice.PostAsync($"/api/me/bookings/{bookingId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Empty(_factory.Payments.Refunded);
    }

    [Fact]
    public async Task StaleRelease_LosesToAConcurrentCancel()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, _) = await BookAsync(alice, showtimeId, KyivA1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await db.Bookings.SingleAsync(b => b.Id == bookingId); // read while still pending
        await alice.PostAsync($"/api/me/bookings/{bookingId}/cancel", null);

        var released = await scope.ServiceProvider.GetRequiredService<BookingService>()
            .ReleaseAsync(stale, BookingStatus.Expired, CancellationToken.None);

        Assert.False(released);
        Assert.Equal(BookingStatus.Cancelled, stale.Status);
    }

    [Fact]
    public async Task PaymentArrivingAfterCancel_IsRefunded()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        var (bookingId, sessionId) = await BookAsync(alice, showtimeId, KyivA1);
        await alice.PostAsync($"/api/me/bookings/{bookingId}/cancel", null);

        _factory.Payments.Pay(sessionId);
        await Webhook("checkout.session.completed", sessionId);

        await UpdateAsync(async db =>
            Assert.Equal(BookingStatus.Refunded, (await db.Bookings.SingleAsync(b => b.Id == bookingId)).Status));
        Assert.Contains($"pi_{sessionId}", _factory.Payments.Refunded);
        Assert.Equal("free", (await SeatStatusesAsync(_factory.CreateClient(), showtimeId))[KyivA1]);
    }

    [Fact]
    public async Task OtherUsersBookings_AreNotVisible()
    {
        var showtimeId = await KyivShowtimeAsync();
        var (bookingId, _) = await BookAsync(await UserAsync("google-alice"), showtimeId, KyivA1);
        var bob = await UserAsync("google-bob");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/me/bookings/{bookingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/me/bookings/{bookingId}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task DeletingAccount_RemovesTheirBookingsAndFreesSeats()
    {
        var showtimeId = await KyivShowtimeAsync();
        var alice = await UserAsync("google-alice");
        await BookAsync(alice, showtimeId, KyivA1);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync("/api/me")).StatusCode);

        Assert.Equal("free", (await SeatStatusesAsync(_factory.CreateClient(), showtimeId))[KyivA1]);
    }
}
