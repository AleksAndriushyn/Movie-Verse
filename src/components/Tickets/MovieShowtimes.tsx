import React, { useState } from "react";
import { Link } from "react-router-dom";
import { useMovieShowtimes } from "@/hooks/useTickets";
import { DateChips, ShowtimeTimes } from "./ShowtimeTimes";
import { formatDateLabel } from "./format";

/** "Get tickets" on a movie page; renders nothing unless the demo cinemas show this movie. */
export const MovieShowtimes: React.FC<{ movieId: number }> = ({ movieId }) => {
	const { data } = useMovieShowtimes(movieId);
	const [chosenDate, setChosenDate] = useState<string | null>(null);

	if (!data || data.showtimes.length === 0) {
		return null;
	}

	const dates = [...new Set(data.showtimes.map((s) => s.date))];
	const date = chosenDate && dates.includes(chosenDate) ? chosenDate : dates[0];

	return (
		<section className="px-4 sm:px-6 lg:px-8 mt-2 mb-10" aria-labelledby="tickets-heading">
			<div className="flex flex-wrap items-baseline justify-between gap-2 mb-4">
				<h2 id="tickets-heading" className="text-3xl font-bold">Get tickets</h2>
				<Link to="/showtimes" className="text-sm text-gray-400 hover:text-white">
					Demo cinemas in San Diego · full schedule
				</Link>
			</div>
			<DateChips dates={dates} selected={date} onSelect={setChosenDate} label={formatDateLabel} />
			<div className="mt-4">
				<ShowtimeTimes cinemas={data.cinemas} showtimes={data.showtimes.filter((s) => s.date === date)} />
			</div>
		</section>
	);
};
