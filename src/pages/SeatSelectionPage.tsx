import React, { useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useParams, useSearchParams } from "react-router-dom";
import { ApiError } from "@/api/AxiosInstance";
import { getLoginUrl } from "@/api/me";
import { DemoBanner } from "@/components/Tickets/DemoBanner";
import { SeatLegend, SeatMap } from "@/components/Tickets/SeatMap";
import { formatMoney, formatShowDate, formatShowTime } from "@/components/Tickets/format";
import { useCurrentUser } from "@/hooks/useAuth";
import { useCancelBooking, useCreateBooking, useMyBooking, useShowtime } from "@/hooks/useTickets";
import type { Seat } from "@/types/tickets";
import { getImageUrl } from "@/utils/image-helper";
import NotFoundPage from "./NotFoundPage";

const MAX_SEATS = 10;

const SeatSelectionPage: React.FC = () => {
	const { showtimeId } = useParams<{ showtimeId: string }>();
	const id = Number(showtimeId);
	const location = useLocation();
	const [searchParams, setSearchParams] = useSearchParams();

	const { data: user } = useCurrentUser();
	const { data: showtime, isLoading, error, refetch } = useShowtime(id);
	const createBooking = useCreateBooking(id);
	const cancelBooking = useCancelBooking();
	const { data: hold } = useMyBooking(showtime?.myHoldBookingId);

	const [selected, setSelected] = useState<Set<number>>(new Set());
	const [message, setMessage] = useState<string | null>(null);

	// Back from Stripe's "cancel" link: release that hold right away instead of after 30 minutes
	const cancelledBooking = searchParams.get("cancelledBooking");
	const handledCancel = useRef(false);
	useEffect(() => {
		if (!cancelledBooking || !user || handledCancel.current) return;
		handledCancel.current = true;
		cancelBooking.mutate(cancelledBooking, {
			onSettled: () => {
				setMessage("Payment cancelled. Your seats were released.");
				setSearchParams({}, { replace: true });
				refetch();
			},
		});
	}, [cancelledBooking, user, cancelBooking, setSearchParams, refetch]);

	const seatsById = useMemo(
		() => new Map(showtime?.rows.flatMap((row) => row.seats.map((seat) => [seat.id, { ...seat, row: row.row }])) ?? []),
		[showtime]
	);

	if (isLoading) {
		return <div className="h-96 rounded-lg bg-gray-800 animate-pulse" />;
	}

	if (error instanceof ApiError && error.status === 404) {
		return <NotFoundPage />;
	}

	if (!showtime) {
		return <p className="text-center p-20 text-red-500">Couldn't load this showtime. Please try again later.</p>;
	}

	const priceCents = (seat: Seat) => (seat.type === "vip" ? showtime.vipPriceCents : showtime.standardPriceCents);
	const price = (seat: Seat) => formatMoney(priceCents(seat), showtime.currency);
	const tz = showtime.cinema.timeZone;

	const selectedSeats = [...selected]
		.map((seatId) => seatsById.get(seatId))
		.filter((seat) => seat !== undefined)
		.sort((a, b) => a.row.localeCompare(b.row) || a.number - b.number);
	const total = selectedSeats.reduce((sum, seat) => sum + priceCents(seat), 0);

	const toggle = (seat: Seat) => {
		setMessage(null);
		setSelected((current) => {
			const next = new Set(current);
			if (next.has(seat.id)) {
				next.delete(seat.id);
			} else if (next.size < MAX_SEATS) {
				next.add(seat.id);
			}
			return next;
		});
	};

	const handleCheckout = () => {
		createBooking.mutate([...selected], {
			onSuccess: ({ checkoutUrl }) => window.location.assign(checkoutUrl),
			onError: (bookingError) => {
				if (bookingError instanceof ApiError && bookingError.status === 409) {
					setMessage("Some of those seats were just taken. The map is updated; please pick again.");
					setSelected(new Set());
					refetch();
				} else {
					setMessage("Couldn't start the payment. Please try again.");
				}
			},
		});
	};

	return (
		<div>
			<title>{`Tickets: ${showtime.movieTitle} | MovieVerse`}</title>
			<DemoBanner />

			<header className="flex gap-4 mb-8">
				{showtime.posterPath && (
					<img src={getImageUrl(showtime.posterPath, "w154")} alt="" className="w-20 sm:w-24 rounded-lg shrink-0 self-start" />
				)}
				<div>
					<h1 className="text-2xl sm:text-3xl font-bold text-white">
						<Link to={`/movie/${showtime.movieId}`} className="hover:text-red-500">{showtime.movieTitle}</Link>
					</h1>
					<p className="text-gray-300 mt-1">
						{formatShowDate(showtime.startsAt, tz)} · {formatShowTime(showtime.startsAt, tz)}{" "}
						<span className="text-gray-500">(San Diego time)</span>
					</p>
					<p className="text-gray-400 text-sm">
						{showtime.cinema.name}, hall {showtime.hallName} · {showtime.cinema.address}
					</p>
					<p className="text-gray-400 text-sm mt-1">
						Standard {formatMoney(showtime.standardPriceCents, showtime.currency)} · VIP{" "}
						{formatMoney(showtime.vipPriceCents, showtime.currency)}
					</p>
				</div>
			</header>

			{hold?.status === "pending" && hold.checkoutUrl && (
				<div role="status" className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-lg bg-gray-800 p-4 text-sm">
					<span className="text-gray-300">
						Seats {hold.seatLabels} are held for you until{" "}
						{hold.holdExpiresAt && formatShowTime(hold.holdExpiresAt, Intl.DateTimeFormat().resolvedOptions().timeZone)}.
					</span>
					<a href={hold.checkoutUrl} className="auth-button w-auto">Finish payment</a>
				</div>
			)}

			{message && <p role="alert" className="mb-6 rounded-lg bg-gray-800 p-4 text-sm text-yellow-200">{message}</p>}

			{!showtime.bookable ? (
				<p className="rounded-lg bg-gray-800 p-6 text-center text-gray-300">Sales for this showtime have closed.</p>
			) : (
				<>
					<SeatMap
						rows={showtime.rows}
						selected={selected}
						onToggle={toggle}
						priceOf={price}
						disabled={selected.size >= MAX_SEATS}
					/>
					<div className="mt-6">
						<SeatLegend />
					</div>

					<div className="sticky bottom-0 mt-8 -mx-4 border-t border-gray-700 bg-gray-900/95 px-4 py-4 backdrop-blur-sm">
						<div className="flex flex-wrap items-center justify-between gap-4">
							<div className="text-sm">
								{selectedSeats.length === 0 ? (
									<span className="text-gray-400">Pick up to {MAX_SEATS} seats.</span>
								) : (
									<>
										<p className="text-white font-semibold">
											{selectedSeats.map((seat) => `${seat.row}${seat.number}`).join(", ")}
										</p>
										<p className="text-gray-400">
											{selectedSeats.length} {selectedSeats.length === 1 ? "seat" : "seats"} ·{" "}
											{formatMoney(total, showtime.currency)}
										</p>
									</>
								)}
							</div>
							{user ? (
								<button
									type="button"
									onClick={handleCheckout}
									disabled={selectedSeats.length === 0 || createBooking.isPending}
									className="auth-button w-auto disabled:opacity-50"
								>
									{createBooking.isPending ? "Holding seats…" : "Continue to payment"}
								</button>
							) : (
								<a href={getLoginUrl(location.pathname)} className="auth-button w-auto">Sign in to book</a>
							)}
						</div>
						{user && selectedSeats.length > 0 && (
							<p className="mt-2 text-xs text-gray-500">Seats are held for 30 minutes while you pay on Stripe.</p>
						)}
					</div>
				</>
			)}
		</div>
	);
};

export default SeatSelectionPage;
