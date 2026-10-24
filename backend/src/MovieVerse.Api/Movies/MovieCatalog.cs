using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using MovieVerse.Api.Tmdb;

namespace MovieVerse.Api.Movies;

/// <summary>
/// Read-only movie data from TMDB, cached server-side so repeated requests
/// don't spend the TMDB quota. Concurrent misses for one key share a single upstream call.
/// </summary>
public sealed class MovieCatalog(TmdbClient tmdb, HybridCache cache)
{
    public static readonly TimeSpan GenresTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan DetailsTtl = TimeSpan.FromHours(1);
    public static readonly TimeSpan ListsTtl = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SearchTtl = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan NowPlayingTtl = TimeSpan.FromHours(6);

    public ValueTask<string> GetPopularAsync(int page, CancellationToken ct) =>
        GetCachedAsync($"popular:{page}", "movie/popular", new() { ["page"] = page.ToString() }, ListsTtl, ct);

    public ValueTask<string> GetDetailsAsync(int movieId, CancellationToken ct) =>
        GetCachedAsync(
            $"movie:{movieId}",
            $"movie/{movieId}",
            new() { ["append_to_response"] = "credits,videos" },
            DetailsTtl,
            ct);

    /// <summary>The card fields of a movie, read from the cached details response.</summary>
    public async Task<MovieSummary> GetSummaryAsync(int movieId, CancellationToken ct)
    {
        var json = await GetDetailsAsync(movieId, ct);
        return JsonSerializer.Deserialize<MovieSummary>(json, TmdbJson.Options)
            ?? throw new InvalidOperationException($"TMDB returned empty details for movie {movieId}.");
    }

    /// <summary>Movies in US cinemas now, with a poster; what the demo cinemas schedule.</summary>
    public async Task<IReadOnlyList<MovieSummary>> GetNowPlayingAsync(CancellationToken ct)
    {
        var json = await GetCachedAsync(
            "now-playing:US", "movie/now_playing", new() { ["region"] = "US", ["page"] = "1" }, NowPlayingTtl, ct);
        var page = JsonSerializer.Deserialize<NowPlayingPage>(json, TmdbJson.Options);
        return page?.Results.Where(m => !string.IsNullOrEmpty(m.PosterPath)).ToList() ?? [];
    }

    private sealed record NowPlayingPage(List<MovieSummary> Results);

    public ValueTask<string> GetTmdbReviewsAsync(int movieId, int page, CancellationToken ct) =>
        GetCachedAsync(
            $"reviews:{movieId}:{page}",
            $"movie/{movieId}/reviews",
            new() { ["page"] = page.ToString() },
            DetailsTtl,
            ct);

    public ValueTask<string> SearchAsync(string query, int page, CancellationToken ct) =>
        GetCachedAsync(
            // TMDB search is case-insensitive, so share cache entries across casing
            $"search:{query.ToLowerInvariant()}:{page}",
            "search/movie",
            new() { ["query"] = query, ["page"] = page.ToString() },
            SearchTtl,
            ct);

    public ValueTask<string> GetGenresAsync(CancellationToken ct) =>
        GetCachedAsync("genres", "genre/movie/list", [], GenresTtl, ct);

    public ValueTask<string> GetByGenreAsync(int genreId, int page, CancellationToken ct) =>
        GetCachedAsync(
            $"genre:{genreId}:{page}",
            "discover/movie",
            new() { ["with_genres"] = genreId.ToString(), ["page"] = page.ToString() },
            ListsTtl,
            ct);

    // Failed upstream calls throw and are never cached
    private ValueTask<string> GetCachedAsync(
        string key,
        string path,
        Dictionary<string, string?> query,
        TimeSpan ttl,
        CancellationToken ct) =>
        cache.GetOrCreateAsync(
            $"tmdb:{key}",
            (tmdb, path, query),
            static (state, token) => new ValueTask<string>(
                state.tmdb.GetJsonAsync(state.path, state.query, token)),
            new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = ttl },
            cancellationToken: ct);
}
