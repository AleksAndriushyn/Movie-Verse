import React from "react";
import { Link, useLocation } from "react-router-dom";
import { getLoginUrl } from "@/api/me";
import { formatShowDate, formatShowTime } from "@/components/Tickets/format";
import { useCurrentUser } from "@/hooks/useAuth";
import { useMyBookings } from "@/hooks/useTickets";
import type { Booking } from "@/types/tickets";
import { getImageUrl } from "@/utils/image-helper";

const BADGES: Record<Booking["status"], string> = {
	pending: "Awaiting payment",
	paid: "Ticket",
	cancelled: "Cancelled",
	expired: "Expired",
	refunded: "Refunded",
};

const BookingRow: React.FC<{ booking: Booking }> = ({ booking }) => (
	<li>
		<Link to={`/tickets/${booking.id}`} className="flex items-center gap-4 rounded-lg bg-gray-800 p-3 hover:bg-gray-700 transition">
			{booking.posterPath ? (
				<img src={getImageUrl(booking.posterPath, "w92")} alt="" className="w-12 rounded shrink-0" />
			) : (
				<span className="w-12 h-18 rounded bg-gray-700 shrink-0" />
			)}
			<div className="min-w-0 flex-1">
				<p className="font-semibold text-white truncate">{booking.movieTitle}</p>
				<p className="text-sm text-gray-400">
					{formatShowDate(booking.startsAt, booking.cinema.timeZone)} · {formatShowTime(booking.startsAt, booking.cinema.timeZone)} ·{" "}
					{booking.cinema.name}
				</p>
				<p className="text-xs text-gray-500">Seats {booking.seatLabels}</p>
			</div>
			<span className="shrink-0 rounded-full bg-gray-700 px-3 py-0.5 text-xs text-gray-200">{BADGES[booking.status]}</span>
		</Link>
	</li>
);

const MyTicketsPage: React.FC = () => {
	const location = useLocation();
	const { data: user, isLoading: isUserLoading } = useCurrentUser();
	const { data: bookings, isLoading, isError } = useMyBookings();

	if (!isUserLoading && !user) {
		return (
			<div className="text-center py-20">
				<title>My tickets | MovieVerse</title>
				<h1 className="text-3xl font-bold text-white mb-2">My tickets</h1>
				<p className="text-gray-400 mb-8">Sign in with Google to book seats and keep your tickets.</p>
				<a href={getLoginUrl(location.pathname)} className="auth-button inline-block w-auto">Sign in</a>
			</div>
		);
	}

	const now = Date.now();
	const upcoming = bookings?.filter((b) => new Date(b.startsAt).getTime() > now) ?? [];
	const past = (bookings?.filter((b) => new Date(b.startsAt).getTime() <= now) ?? []).reverse();

	return (
		<div className="max-w-3xl mx-auto">
			<title>My tickets | MovieVerse</title>
			<h1 className="text-3xl font-bold text-white mb-8">My tickets</h1>
			{(isUserLoading || isLoading) && <div className="h-32 rounded-lg bg-gray-800 animate-pulse" />}
			{isError && <p className="text-red-400">Couldn't load your tickets. Please try again later.</p>}
			{bookings && bookings.length === 0 && (
				<p className="text-gray-400">
					No tickets yet. <Link to="/showtimes" className="text-red-400 hover:text-red-300 underline">See what's playing</Link>.
				</p>
			)}
			{upcoming.length > 0 && (
				<section className="mb-10">
					<h2 className="text-xl font-semibold text-white mb-3">Upcoming</h2>
					<ul className="space-y-3">{upcoming.map((b) => <BookingRow key={b.id} booking={b} />)}</ul>
				</section>
			)}
			{past.length > 0 && (
				<section>
					<h2 className="text-xl font-semibold text-white mb-3">Past</h2>
					<ul className="space-y-3 opacity-80">{past.map((b) => <BookingRow key={b.id} booking={b} />)}</ul>
				</section>
			)}
		</div>
	);
};

export default MyTicketsPage;
