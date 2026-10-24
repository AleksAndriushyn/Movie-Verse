import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import Layout from "./components/Layout/Layout";
import HomePage from "./pages/HomePage";
import { Route, Routes } from "react-router-dom";
import MoviePage from "./pages/MoviePage";
import SearchPage from "./pages/SearchPage";
import GenrePage from "./pages/GenrePage";
import NotFoundPage from "./pages/NotFoundPage";
import ListPage from "./pages/ListPage";
import { lazy, Suspense } from "react";

// Rarely visited pages load on demand, keeping the main bundle small
const PrivacyPage = lazy(() => import("./pages/PrivacyPage"));
const ModerationPage = lazy(() => import("./pages/ModerationPage"));
const SchedulePage = lazy(() => import("./pages/SchedulePage"));
const SeatSelectionPage = lazy(() => import("./pages/SeatSelectionPage"));
const TicketPage = lazy(() => import("./pages/TicketPage"));
const MyTicketsPage = lazy(() => import("./pages/MyTicketsPage"));
import { SpeedInsights } from "@vercel/speed-insights/react"
import { ApiError } from "./api/AxiosInstance";

const MAX_RETRIES = 2;

const queryClient = new QueryClient({
	defaultOptions: {
		queries: {
			// 4xx (e.g. unknown movie id) won't succeed on retry, so fail fast
			retry: (failureCount, error) => {
				if (error instanceof ApiError && error.status && error.status < 500) {
					return false;
				}
				return failureCount < MAX_RETRIES;
			},
		},
	},
});

const App: React.FC = () => {
	return (
		<QueryClientProvider client={queryClient}>
			<Layout>
				<Suspense fallback={<div className="h-64 rounded-lg bg-gray-800 animate-pulse" />}>
					<Routes>
						<Route path="/" element={<HomePage />} />
						<Route path="/movie/:movieId" element={<MoviePage />} />
						<Route path="/search" element={<SearchPage />} />
						<Route path="/genre/:genreId" element={<GenrePage />} />
						<Route
							path="/favorites"
							element={<ListPage list="favorites" title="Favorites" emptyMessage="No favorites yet. Tap the heart on any movie to add it." />}
						/>
						<Route
							path="/watchlist"
							element={<ListPage list="watchlist" title="Watchlist" emptyMessage="Your watchlist is empty. Tap the bookmark on any movie to save it for later." />}
						/>
						<Route
							path="/ratings"
							element={<ListPage list="ratings" title="Your Ratings" emptyMessage="You haven't rated any movies yet. Open a movie and pick from 1 to 10 stars." />}
						/>
						<Route path="/showtimes" element={<SchedulePage />} />
						<Route path="/showtimes/:showtimeId" element={<SeatSelectionPage />} />
						<Route path="/tickets" element={<MyTicketsPage />} />
						<Route path="/tickets/:bookingId" element={<TicketPage />} />
						<Route path="/admin/reviews" element={<ModerationPage />} />
						<Route path="/privacy" element={<PrivacyPage />} />
						<Route path="*" element={<NotFoundPage />} />
					</Routes>
				</Suspense>
				<SpeedInsights/>
			</Layout>
		</QueryClientProvider>
	);
};

export default App;
