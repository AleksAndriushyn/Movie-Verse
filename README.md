# MovieVerse

A movie browsing site with personal lists, ratings and reviews. Movie data comes from
[TMDB](https://www.themoviedb.org/).

**Live:** https://movies-world-lovat.vercel.app

## Features

- Popular movies, movies by genre and search, with pagination in the URL
- Movie page: details, director and writers, cast, trailers (YouTube), reviews
- Sign in with Google; favorites and watchlist; your own 1–10 ratings
- Reviews: one per movie, editable, with a spoiler flag; reviews from TMDB in a separate tab
- Reports and moderation: three reports hide a review until a moderator keeps or removes it (`/admin/reviews`)
- Demo cinema tickets: two fictional cinemas in San Diego (Flamingo Screens and Potuzhniy Cinema) show this week's
  US releases; pick seats on a hall map, pay through Stripe Checkout in test mode, get a ticket with a QR code, cancel
  for a test refund (`/showtimes`, `/tickets`)
- Privacy policy (`/privacy`) and self-service account deletion

## Architecture

```
Browser ──► Vercel: React SPA ── /api/* rewrite ──► Azure App Service: ASP.NET Core 10 API
                                                       ├─► TMDB API (token stays on the server, responses cached)
                                                       ├─► Azure SQL (users, lists, ratings, reviews, bookings)
                                                       └─► Stripe (test mode checkout and refunds)
```

The frontend only talks to the same-origin `/api`. The API keeps the TMDB token, caches TMDB responses,
handles Google sign-in with a cookie session and stores user data. See [backend/README.md](backend/README.md)
for the endpoints and deployment.

| Part | Stack |
| --- | --- |
| Frontend | React 19, TypeScript, Vite 7, React Router 7, TanStack Query 5, Tailwind CSS 4, Headless UI, Embla Carousel |
| API | ASP.NET Core 10 Minimal API, EF Core 10, HybridCache, Google OAuth, Stripe.net, rate limiting, xUnit integration tests |
| Hosting | Vercel (frontend), Azure App Service Free F1 and Azure SQL free serverless database (API), GitHub Actions (API deploys) |

## Getting started

Prerequisites: Node.js 20.19+ or 22.12+, the .NET 10 SDK, and for the API's development database the Azure CLI
signed in with access to it (`az login`).

```bash
npm ci
```

Configure the API once (TMDB token, Google OAuth client, Stripe test key, database access) as described in
[backend/README.md → Local development](backend/README.md#local-development), then run the API and the site together:

```bash
npm run dev:all
```

The site runs at http://localhost:5173 and proxies `/api` to the API at http://localhost:5080.
The frontend itself needs no secrets.

| Script | What it does |
| --- | --- |
| `npm run dev` | Frontend only (Vite dev server) |
| `npm run dev:api` | API only (`dotnet run`) |
| `npm run dev:all` | API and frontend together |
| `npm run build` | Type-check and build the frontend into `dist/` |
| `npm run lint` | ESLint |
| `npm run preview` | Serve the production build locally |

API tests: `dotnet test backend` (in-memory SQLite and a fake TMDB, no network needed).

## Project structure

```
src/                 React app
  api/               API calls (movies, account, lists, reviews, tickets)
  components/        UI components by feature (Movies, Reviews, Tickets, Lists, Ratings, Auth, Layout, ...)
  hooks/             TanStack Query hooks
  pages/             Routes: home, movie, search, genre, showtimes, seat map, tickets, lists, moderation, privacy, 404
  types/, utils/
backend/
  src/MovieVerse.Api/          ASP.NET Core API (Movies, Auth, Lists, Reviews, Tickets, Payments, Data + migrations)
  tests/MovieVerse.Api.Tests/  Integration tests
.github/workflows/   Backend build, test and deploy
vercel.json          SPA fallback and the /api rewrite to Azure
```

## Deployment

- **Frontend:** Vercel builds and deploys every push to `main`; pull requests get preview deployments.
- **API:** the `Backend` GitHub Actions workflow tests every pull request that touches `backend/` and deploys
  `main` to Azure App Service. Database migrations run when the API starts.
- **Moderators:** admin rights are a flag in the database, set with
  `UPDATE Users SET IsAdmin = 1 WHERE Email = '<email>'`; the user signs in again to see the Moderation link.

## Attribution

This product uses the TMDB API but is not endorsed or certified by TMDB.
