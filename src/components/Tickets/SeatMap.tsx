import React from "react";
import clsx from "clsx";
import type { Seat, SeatRow, SeatType } from "@/types/tickets";

const SEAT_SIZE = "w-7 h-7 sm:w-8 sm:h-8";

const TYPE_NAMES: Record<SeatType, string> = {
	standard: "Standard",
	vip: "VIP",
	wheelchair: "Wheelchair space",
};

const seatClass = (seat: Seat, selected: boolean, disabled: boolean) =>
	clsx(
		SEAT_SIZE,
		"rounded-t-lg rounded-b text-[10px] sm:text-xs font-semibold transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-white",
		selected
			? "bg-red-600 text-white"
			: seat.status === "taken"
				? "bg-gray-800 text-gray-600 cursor-not-allowed"
				: seat.type === "vip"
					? "bg-purple-700 text-purple-100 hover:bg-purple-600"
					: seat.type === "wheelchair"
						? "bg-sky-700 text-sky-100 hover:bg-sky-600"
						: "bg-gray-600 text-gray-100 hover:bg-gray-500",
		disabled && !selected && seat.status !== "taken" && "opacity-60 cursor-not-allowed"
	);

type SeatMapProps = {
	rows: SeatRow[];
	selected: ReadonlySet<number>;
	onToggle: (seat: Seat) => void;
	priceOf: (seat: Seat) => string;
	disabled: boolean;
};

/** The hall as seen from the back: screen on top, rows A..Z below, aisles kept as gaps. */
export const SeatMap: React.FC<SeatMapProps> = ({ rows, selected, onToggle, priceOf, disabled }) => {
	// rows are centred: a row narrower than the widest one starts further in
	const width = (row: SeatRow) => Math.max(...row.seats.map((seat) => seat.column)) + 1;
	const hallWidth = Math.max(...rows.map(width));

	return (
		<div className="overflow-x-auto pb-2">
			<div className="mx-auto w-max min-w-full">
				<div className="mx-auto mb-8 h-2 w-3/4 rounded-full bg-gradient-to-r from-transparent via-gray-300 to-transparent" />
				<p className="mb-6 text-center text-xs uppercase tracking-[0.3em] text-gray-500">Screen</p>
				<div className="space-y-2" role="group" aria-label="Seats">
					{rows.map((row) => {
						const offset = Math.floor((hallWidth - width(row)) / 2);
						return (
							<div key={row.row} className="flex items-center gap-2">
								<span className="w-5 text-center text-xs text-gray-500" aria-hidden="true">{row.row}</span>
								<div
									className="grid gap-1 sm:gap-1.5"
									style={{ gridTemplateColumns: `repeat(${hallWidth}, auto)` }}
								>
									{row.seats.map((seat) => {
										const isSelected = selected.has(seat.id);
										return (
											<button
												key={seat.id}
												type="button"
												style={{ gridColumnStart: seat.column + offset + 1 }}
												className={seatClass(seat, isSelected, disabled)}
												disabled={seat.status === "taken" || (disabled && !isSelected)}
												aria-pressed={isSelected}
												aria-label={`Row ${row.row}, seat ${seat.number}, ${TYPE_NAMES[seat.type]}, ${
													seat.status === "taken" ? "taken" : priceOf(seat)
												}`}
												title={`${row.row}${seat.number} · ${TYPE_NAMES[seat.type]} · ${
													seat.status === "taken" ? "taken" : priceOf(seat)
												}`}
												onClick={() => onToggle(seat)}
											>
												{seat.type === "wheelchair" ? "♿" : seat.number}
											</button>
										);
									})}
								</div>
								<span className="w-5 text-center text-xs text-gray-500" aria-hidden="true">{row.row}</span>
							</div>
						);
					})}
				</div>
			</div>
		</div>
	);
};

export const SeatLegend: React.FC = () => (
	<ul className="flex flex-wrap justify-center gap-x-5 gap-y-2 text-xs text-gray-400">
		{[
			["bg-gray-600", "Standard"],
			["bg-purple-700", "VIP"],
			["bg-sky-700", "Wheelchair"],
			["bg-red-600", "Your selection"],
			["bg-gray-800", "Taken"],
		].map(([color, label]) => (
			<li key={label} className="flex items-center gap-2">
				<span className={clsx("inline-block h-4 w-4 rounded-t-md rounded-b-sm", color)} aria-hidden="true" />
				{label}
			</li>
		))}
	</ul>
);
