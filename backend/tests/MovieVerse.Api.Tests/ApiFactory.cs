using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieVerse.Api.Auth;
using MovieVerse.Api.Data;
using MovieVerse.Api.Payments;
using MovieVerse.Api.Tmdb;

namespace MovieVerse.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AccessToken = "test-token";

    // One in-memory SQLite database per factory; it lives as long as this connection is open
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakeTmdbHandler Tmdb { get; } = new();

    public FakePaymentGateway Payments { get; } = new();

    /// <summary>Extra configuration a test needs, e.g. App:PublicOrigin.</summary>
    public Dictionary<string, string?> Settings { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");
        builder.UseSetting("Tmdb:AccessToken", AccessToken);
        builder.UseSetting("Google:ClientId", "test-client-id");
        builder.UseSetting("Google:ClientSecret", "test-client-secret");
        builder.UseSetting("ConnectionStrings:Default", "unused; replaced by SQLite below");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Stripe:SecretKey", "sk_test_fake");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_fake");
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<TmdbClient>().ConfigurePrimaryHttpMessageHandler(() => Tmdb);
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Payments);

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            // Authenticate requests carrying X-Test-User; challenges still go to the real cookie scheme
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
            services.PostConfigure<AuthenticationOptions>(options => options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }

    public async Task<User> AddUserAsync(string subject, string name = "Test User")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { GoogleSubject = subject, Name = name, Email = $"{subject}@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public HttpClient CreateClientFor(User user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, user.Id.ToString());
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, userId.ToString()),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(AuthClaims.Picture, "https://example.com/avatar.png"),
            ],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
