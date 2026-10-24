using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieVerse.Api.Auth;
using MovieVerse.Api.Data;
using MovieVerse.Api.Infrastructure;
using MovieVerse.Api.Lists;
using MovieVerse.Api.Movies;
using MovieVerse.Api.Payments;
using MovieVerse.Api.Reviews;
using MovieVerse.Api.Tickets;
using MovieVerse.Api.Tmdb;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
// enums as "visible" / "spam" rather than numbers
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddExceptionHandler<UpstreamExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddHybridCache();

builder.Services
    .AddOptions<TmdbOptions>()
    .Bind(builder.Configuration.GetSection(TmdbOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddHttpClient<TmdbClient>((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<TmdbOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
    })
    // retries transient 5xx/408/429 with backoff, plus timeouts and a circuit breaker
    .AddStandardResilienceHandler();

builder.Services.AddScoped<MovieCatalog>();
builder.Services.AddScoped<ShowtimeScheduler>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddOptions<TicketsOptions>().Bind(builder.Configuration.GetSection(TicketsOptions.SectionName));

builder.Services
    .AddOptions<StripeOptions>()
    .Bind(builder.Configuration.GetSection(StripeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IPaymentGateway, StripePaymentGateway>();

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'Default' is not configured. Locally it comes from appsettings.Development.json; " +
        "on Azure, add the connection string setting ConnectionStrings__Default.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    // the free serverless database pauses when idle; retries cover the resume
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "mv_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // Google sign-in doesn't mark the session persistent, which would make it a browser-session
        // cookie that disappears on restart; give every session the 30-day expiry instead
        options.Events.OnSigningIn = context =>
        {
            context.Properties.IsPersistent = true;
            return Task.CompletedTask;
        };
        // an API: answer 401/403 instead of redirecting to a login page
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddGoogle(options =>
    {
        builder.Configuration.GetSection("Google").Bind(options);
        options.CallbackPath = "/api/auth/signin-google";
        options.ClaimActions.MapJsonKey(AuthClaims.Picture, "picture");
        // Google's callback is a top-level GET, so Lax works and also survives http://localhost
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.Events.OnCreatingTicket = UserProvisioning.OnCreatingTicketAsync;
        // e.g. the user cancelled on Google's consent screen
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/?loginError=1");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });

builder.Services
    .AddOptions<GoogleOptions>(GoogleDefaults.AuthenticationScheme)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ClientId) && !string.IsNullOrWhiteSpace(options.ClientSecret),
        "Google sign-in is not configured. Locally run: dotnet user-secrets set \"Google:ClientId\" \"<id>\" and " +
        "\"Google:ClientSecret\" \"<secret>\" --project backend/src/MovieVerse.Api. " +
        "On Azure, add the app settings Google__ClientId and Google__ClientSecret.")
    .ValidateOnStart();

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientIp(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Behind the Vercel rewrite the API sees Azure's host name; use the site's public origin
// so the Google redirect_uri and cookie Secure flag match what the browser sees
if (Uri.TryCreate(app.Configuration["App:PublicOrigin"], UriKind.Absolute, out var publicOrigin))
{
    // no explicit :443, or redirect_uri wouldn't match the one registered with Google
    var publicHost = publicOrigin.IsDefaultPort
        ? new HostString(publicOrigin.Host)
        : new HostString(publicOrigin.Host, publicOrigin.Port);
    app.Use((context, next) =>
    {
        context.Request.Scheme = publicOrigin.Scheme;
        context.Request.Host = publicHost;
        return next(context);
    });
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health").DisableRateLimiting();
app.MapMovieEndpoints();
app.MapAuthEndpoints();
app.MapListEndpoints();
app.MapRatingEndpoints();
app.MapReviewEndpoints();
app.MapModerationEndpoints();
app.MapShowtimeEndpoints();
app.MapBookingEndpoints();

app.Run();

// Requests arrive through the Vercel rewrite and Azure's front end, so the
// client IP is the first X-Forwarded-For entry rather than the socket address
static string GetClientIp(HttpContext http)
{
    var forwardedFor = http.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrEmpty(forwardedFor))
    {
        return forwardedFor.Split(',')[0].Trim();
    }

    return http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public partial class Program;
