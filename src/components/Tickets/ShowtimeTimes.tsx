import React from "react";
import { Link } from "react-router-dom";
import clsx from "clsx";
import type { Cinema, ShowtimeSlot } from "@/types/tickets";
import { formatShowTime } from "./format";

/** Time buttons grouped by cinema, each leading to its seat map. */
export const ShowtimeTimes: React.FC<{ cinemas: Cinema[]; showtimes: ShowtimeSlot[] }> = ({ cinemas, showtimes }) => (
	<div className="space-y-3">
		{cinemas.map((cinema) => {
			const slots = showtimes.filter((s) => s.cinemaId === cinema.id);
			if (slots.length === 0) return null;
			return (
				<div key={cinema.id}>
					<p className="text-sm text-gray-400 mb-1.5">
						<span className="font-semibold text-gray-200">{cinema.name}</span> · {cinema.address}
					</p>
					<div className="flex flex-wrap gap-2">
						{slots.map((slot) =>
							slot.bookable ? (
								<Link
									key={slot.id}
									to={`/showtimes/${slot.id}`}
									className="rounded-lg border border-gray-600 px-3 py-1.5 text-sm font-semibold text-white hover:border-red-500 hover:bg-red-600/20"
									title={`Hall ${slot.hallName}`}
								>
									{formatShowTime(slot.startsAt, cinema.timeZone)}
									<span className="ml-1 text-xs font-normal text-gray-400">{slot.hallName}</span>
								</Link>
							) : (
								<span
									key={slot.id}
									className="rounded-lg border border-gray-800 px-3 py-1.5 text-sm text-gray-600 line-through"
									title="Sales closed"
								>
									{formatShowTime(slot.startsAt, cinema.timeZone)}
								</span>
							)
						)}
					</div>
				</div>
			);
		})}
	</div>
);

export const DateChips: React.FC<{ dates: string[]; selected: string; onSelect: (date: string) => void; label: (date: string) => string }> = ({
	dates,
	selected,
	onSelect,
	label,
}) => (
	<div className="flex gap-2 overflow-x-auto pb-2" role="tablist" aria-label="Date">
		{dates.map((date) => (
			<button
				key={date}
				type="button"
				role="tab"
				aria-selected={date === selected}
				onClick={() => onSelect(date)}
				className={clsx(
					"shrink-0 rounded-full px-4 py-1.5 text-sm font-semibold",
					date === selected ? "bg-red-600 text-white" : "bg-gray-800 text-gray-300 hover:text-white"
				)}
			>
				{label(date)}
			</button>
		))}
	</div>
);
