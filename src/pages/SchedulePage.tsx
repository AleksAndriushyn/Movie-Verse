import React from "react";
import { Link, useSearchParams } from "react-router-dom";
import { DemoBanner } from "@/components/Tickets/DemoBanner";
import { DateChips, ShowtimeTimes } from "@/components/Tickets/ShowtimeTimes";
import { formatDateLabel } from "@/components/Tickets/format";
import { useSchedule } from "@/hooks/useTickets";
import { getImageUrl } from "@/utils/image-helper";

const SchedulePage: React.FC = () => {
	const [searchParams, setSearchParams] = useSearchParams();
	const requestedDate = searchParams.get("date");
	const { data, isLoading, isError, isFetching } = useSchedule(requestedDate);

	const selectDate = (date: string) => setSearchParams(date === data?.dates[0] ? {} : { date });

	return (
		<div className="max-w-5xl mx-auto">
			<title>Showtimes | MovieVerse</title>
			<h1 className="text-3xl sm:text-4xl font-bold text-white mb-2">Showtimes</h1>
			<p className="text-gray-400 mb-6">Now playing at our cinemas in San Diego. Times are San Diego time.</p>
			<DemoBanner />

			{data && <DateChips dates={data.dates} selected={data.date} onSelect={selectDate} label={formatDateLabel} />}

			{isLoading && <div className="mt-6 h-64 rounded-lg bg-gray-800 animate-pulse" />}
			{isError && <p className="mt-6 text-red-400">Couldn't load the schedule. Please try again later.</p>}
			{data && data.movies.length === 0 && (
				<p className="mt-6 text-gray-400">Nothing scheduled for this day yet. Try another date.</p>
			)}

			<ul className={isFetching ? "mt-6 space-y-6 opacity-60 transition-opacity" : "mt-6 space-y-6 transition-opacity"}>
				{data?.movies.map((movie) => (
					<li key={movie.movieId} className="flex gap-4 rounded-lg bg-gray-800/60 p-4">
						<Link to={`/movie/${movie.movieId}`} className="shrink-0">
							{movie.posterPath ? (
								<img src={getImageUrl(movie.posterPath, "w154")} alt={movie.title} loading="lazy" className="w-20 sm:w-24 rounded-lg" />
							) : (
								<span className="block w-20 sm:w-24 aspect-[2/3] rounded-lg bg-gray-700" />
							)}
						</Link>
						<div className="min-w-0 flex-1">
							<h2 className="text-lg font-semibold text-white mb-3">
								<Link to={`/movie/${movie.movieId}`} className="hover:text-red-500">{movie.title}</Link>
							</h2>
							<ShowtimeTimes cinemas={data.cinemas} showtimes={movie.showtimes} />
						</div>
					</li>
				))}
			</ul>
		</div>
	);
};

export default SchedulePage;
