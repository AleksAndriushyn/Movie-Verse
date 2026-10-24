# MovieVerse API

ASP.NET Core 10 API behind the MovieVerse frontend. It proxies the TMDB endpoints the site uses,
so the TMDB token never reaches the browser, and keeps user accounts (Google sign-in) with their
favorites, watchlist, ratings and reviews in Azure SQL.

| Endpoint | TMDB call | Cached for |
| --- | --- | --- |
| `GET /api/movies/popular?page=` | `movie/popular` | 15 min |
| `GET /api/movies/search?query=&page=` | `search/movie` | 5 min |
| `GET /api/movies/{id}` | `movie/{id}?append_to_response=credits,videos` | 1 h |
| `GET /api/genres` | `genre/movie/list` | 24 h |
| `GET /api/genres/{id}/movies?page=` | `discover/movie?with_genres=` | 15 min |
| `GET /api/movies/{id}/tmdb-reviews?page=` | `movie/{id}/reviews` | 1 h |
| `GET /health` | — | — |

Account endpoints use a cookie session (`mv_auth`: HttpOnly, SameSite=Lax, 30 days, renewed while in use):

| Endpoint | Description |
| --- | --- |
| `GET /api/auth/login?returnUrl=` | Redirects to Google; returns to `returnUrl` (same-site paths only) |
| `POST /api/auth/logout` | Ends the session |
| `GET /api/me` | Current user, or 401 |
| `DELETE /api/me` | Deletes the account with its lists, ratings, reviews and reports, and ends the session |
| `GET /api/me/lists` | Movie ids in `favorites` and `watchlist`, plus `ratings` as `{ "movieId": score }` |
| `GET /api/me/lists/{list}?page=` | A list page in TMDB's paginated shape, newest first |
| `PUT` / `DELETE /api/me/lists/{list}/{movieId}` | Add (idempotent, up to 1000 per list) / remove |
| `GET /api/me/ratings?page=` | Rated movies in TMDB's paginated shape with `user_rating`, newest first |
| `PUT /api/me/ratings/{movieId}` | Rate with body `{ "score": 1-10 }`; re-rating updates the score (up to 5000 ratings) |
| `DELETE /api/me/ratings/{movieId}` | Clears the rating |

Reviews are public; writing, editing and reporting need a session:

| Endpoint | Description |
| --- | --- |
| `GET /api/movies/{id}/reviews?page=` | Visible reviews, newest first, 10 per page, with the author's name, picture and rating; for a signed-in reader also `isOwn` and `reportedByMe` |
| `GET /api/me/reviews/{movieId}` | Your review in any status (`visible`, `hidden`, `removed`), or `{ "review": null }` |
| `PUT /api/me/reviews/{movieId}` | Write or edit with `{ "body": "...", "containsSpoilers": false }`: 10–4000 characters, one review per movie, at most 20 new reviews a day |
| `DELETE /api/me/reviews/{movieId}` | Deletes your review (not one a moderator removed) |
| `POST /api/reviews/{reviewId}/reports` | Report with `{ "reason": "spam" \| "offensive" \| "spoilers" \| "other" }`; once per user; 3 reports hide the review |

Moderation endpoints need `Users.IsAdmin`, which is checked in the database on every request
(grant it with `UPDATE Users SET IsAdmin = 1 WHERE Email = '<email>'`):

| Endpoint | Description |
| --- | --- |
| `GET /api/admin/reviews?page=` | Hidden reviews and visible ones with reports, with movie title, author email and report reasons |
| `POST /api/admin/reviews/{reviewId}/restore` | Makes the review visible and clears its reports |
| `POST /api/admin/reviews/{reviewId}/remove` | Removes the review for good; the author can't edit or re-post it |

TMDB proxy responses are TMDB's JSON unchanged, and list and rating pages use the same snake_case shape so the
frontend reuses its movie grids; the other endpoints use camelCase JSON with enums as strings. Errors are
[ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457).
`page` must be 1–500 and `query` at most 100 characters. Requests are rate limited to 120 per minute per client IP.
The OpenAPI document is served at `/openapi/v1.json` in Development.

## Local development

Requires the .NET 10 SDK and the Azure CLI. From the repository root, store your
[TMDB API Read Access Token](https://www.themoviedb.org/settings/api) and the Google OAuth client in user secrets once:

```bash
dotnet user-secrets set "Tmdb:AccessToken" "<token>" --project backend/src/MovieVerse.Api
dotnet user-secrets set "Google:ClientId" "<client id>" --project backend/src/MovieVerse.Api
dotnet user-secrets set "Google:ClientSecret" "<client secret>" --project backend/src/MovieVerse.Api
```

The development database is `movieverse-dev` on Azure SQL (see `appsettings.Development.json`). It uses
Microsoft Entra sign-in, so there is no password: run `az login` as an account with access to the server,
and allow your IP once (it changes when you switch networks):

```bash
az sql server firewall-rule create -g movieverse-rg -s movieverse-sql-d94a9b -n dev-machine --start-ip-address <ip> --end-ip-address <ip>
```

Migrations live in `Data/Migrations` and are applied on startup. To add one:

```bash
dotnet ef migrations add <Name> --project backend/src/MovieVerse.Api --output-dir Data/Migrations
```

Then run the API (http://localhost:5080) and the frontend together:

```bash
npm run dev:all
```

The Vite dev server proxies `/api` to the API. Run the tests with `dotnet test backend`; they use
in-memory SQLite and a fake TMDB, so they need neither Azure nor network access.

## Deployment (Azure App Service, Free F1)

1. Create a **Web App**: Publish *Code*, Runtime stack *.NET 10 (LTS)*, OS *Linux*, pricing plan *Free F1*.
2. In the app's **Settings → Environment variables**, add `Tmdb__AccessToken`, `Google__ClientId`,
   `Google__ClientSecret`, `App__PublicOrigin` (the site's URL, used for the Google redirect URI) and
   `ConnectionStrings__Default` (Azure SQL with `Authentication=Active Directory Default`). Turn on the app's
   system-assigned managed identity and create a database user for it
   (`CREATE USER [<app name>] FROM EXTERNAL PROVIDER`, roles `db_datareader`, `db_datawriter`, `db_ddladmin`).
3. In **Settings → Configuration**, turn on *SCM Basic Auth Publishing Credentials*, then download the
   publish profile from the app's **Overview** page.
4. In the GitHub repository, add the secret `AZURE_WEBAPP_PUBLISH_PROFILE` (the publish profile file's contents)
   and the variable `AZURE_WEBAPP_NAME` (the app name). The `Backend` workflow deploys every push to `main`
   that touches `backend/`; run it manually the first time.
5. Point the frontend at the API: the `/api/:path*` rewrite in `vercel.json` forwards to the app's
   default domain (currently `movieverse-api-c0e3fe.azurewebsites.net`).

The Free tier sleeps when idle, so the first request after a pause takes a few seconds. The free
serverless database pauses after an hour without queries and takes up to a minute to resume; only
signed-in features touch it, and the API retries while it wakes up.
