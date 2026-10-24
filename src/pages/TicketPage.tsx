import React, { useState } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import { QRCodeSVG } from "qrcode.react";
import { ApiError } from "@/api/AxiosInstance";
import { getLoginUrl } from "@/api/me";
import { DemoBanner } from "@/components/Tickets/DemoBanner";
import { formatMoney, formatShowDate, formatShowTime } from "@/components/Tickets/format";
import { ConfirmDialog } from "@/components/UI/ConfirmDialog";
import { useCurrentUser } from "@/hooks/useAuth";
import { useCancelBooking, useMyBooking } from "@/hooks/useTickets";
import type { Booking } from "@/types/tickets";
import { getImageUrl } from "@/utils/image-helper";
import NotFoundPage from "./NotFoundPage";

const STATUS_TEXT: Record<Booking["status"], string> = {
	pending: "Waiting for payment",
	paid: "Paid",
	cancelled: "Cancelled",
	expired: "Expired: the seats were released",
	refunded: "Refunded",
};

const Detail: React.FC<{ label: string; children: React.ReactNode }> = ({ label, children }) => (
	<div>
		<dt className="text-xs uppercase tracking-wide text-gray-500">{label}</dt>
		<dd className="text-white font-semibold">{children}</dd>
	</div>
);

const TicketPage: React.FC = () => {
	const { bookingId } = useParams<{ bookingId: string }>();
	const location = useLocation();
	const { data: user, isLoading: isUserLoading } = useCurrentUser();
	const { data: booking, isLoading, error } = useMyBooking(bookingId);
	const cancel = useCancelBooking();
	const [confirming, setConfirming] = useState(false);

	if (isUserLoading || isLoading) {
		return <div className="max-w-xl mx-auto h-96 rounded-2xl bg-gray-800 animate-pulse" />;
	}

	if (!user) {
		return (
			<div className="text-center py-20">
				<p className="text-gray-400 mb-6">Sign in to see your ticket.</p>
				<a href={getLoginUrl(location.pathname)} className="auth-button inline-block w-auto">Sign in</a>
			</div>
		);
	}

	if (error instanceof ApiError && error.status === 404) {
		return <NotFoundPage />;
	}

	if (!booking) {
		return <p className="text-center p-20 text-red-500">Couldn't load this booking. Please try again later.</p>;
	}

	const tz = booking.cinema.timeZone;
	const isRefund = booking.status === "paid";


	return (
		<div className="max-w-xl mx-auto">
			<title>{`Ticket: ${booking.movieTitle} | MovieVerse`}</title>
			<DemoBanner />

			<article className="overflow-hidden rounded-2xl bg-gray-800 shadow-2xl">
				<header className="flex gap-4 p-6">
					{booking.posterPath && (
						<img src={getImageUrl(booking.posterPath, "w154")} alt="" className="w-20 rounded-lg shrink-0 self-start" />
					)}
					<div className="min-w-0">
						<h1 className="text-2xl font-bold text-white">
							<Link to={`/movie/${booking.movieId}`} className="hover:text-red-500">{booking.movieTitle}</Link>
						</h1>
						<p className="mt-1 text-sm text-gray-400">{booking.cinema.name} · {booking.cinema.address}</p>
						<p
							className={
								booking.status === "paid"
									? "mt-2 inline-block rounded-full bg-green-800 px-3 py-0.5 text-xs font-semibold text-green-100"
									: "mt-2 inline-block rounded-full bg-gray-700 px-3 py-0.5 text-xs font-semibold text-gray-200"
							}
						>
							{STATUS_TEXT[booking.status]}
						</p>
					</div>
				</header>

				<dl className="grid grid-cols-2 gap-4 border-t border-dashed border-gray-600 p-6 sm:grid-cols-4">
					<Detail label="Date">{formatShowDate(booking.startsAt, tz)}</Detail>
					<Detail label="Time">{formatShowTime(booking.startsAt, tz)}</Detail>
					<Detail label="Hall">{booking.hallName}</Detail>
					<Detail label="Seats">{booking.seatLabels}</Detail>
					<Detail label="Total">{formatMoney(booking.totalCents, booking.currency)}</Detail>
				</dl>

				{booking.status === "paid" && booking.ticketCode && (
					<div className="flex flex-col items-center gap-3 border-t border-dashed border-gray-600 p-6">
						<div className="rounded-xl bg-white p-3">
							<QRCodeSVG value={`MOVIEVERSE:${booking.ticketCode}:${booking.id}`} size={176} />
						</div>
						<p className="font-mono text-2xl tracking-[0.3em] text-white">{booking.ticketCode}</p>
						<p className="text-xs text-gray-500">Show this code at the entrance (of a cinema that doesn't exist).</p>
					</div>
				)}

				{booking.status === "pending" && (
					<div className="border-t border-dashed border-gray-600 p-6 text-center text-sm text-gray-300 space-y-3">
						<p>Confirming your payment with Stripe…</p>
						{booking.checkoutUrl && (
							<p>
								Haven't paid yet?{" "}
								<a href={booking.checkoutUrl} className="text-red-400 hover:text-red-300 underline">Finish payment</a>
							</p>
						)}
					</div>
				)}
			</article>

			<div className="mt-6 flex flex-wrap items-center justify-between gap-4 text-sm">
				<Link to="/tickets" className="text-gray-400 hover:text-white">All my tickets</Link>
				{booking.canCancel && (
					<button
						type="button"
						onClick={() => setConfirming(true)}
						disabled={cancel.isPending}
						className="text-red-400 hover:text-red-300 disabled:opacity-50"
					>
						{isRefund ? "Cancel and refund" : "Cancel booking"}
					</button>
				)}
				{booking.status === "paid" && !booking.canCancel && (
					<span className="text-gray-500">Refunds close an hour before the showtime.</span>
				)}
			</div>
			<ConfirmDialog
				open={confirming}
				title={isRefund ? "Cancel and refund this ticket?" : "Cancel this booking?"}
				confirmLabel={isRefund ? "Cancel and refund" : "Cancel booking"}
				destructive
				pending={cancel.isPending}
				error={cancel.isError ? cancel.error.message : null}
				onConfirm={() => cancel.mutate(booking.id, { onSuccess: () => setConfirming(false) })}
				onClose={() => {
					setConfirming(false);
					cancel.reset();
				}}
			>
				{isRefund
					? `The (test) payment of ${formatMoney(booking.totalCents, booking.currency)} will be refunded and seats ${booking.seatLabels} released.`
					: `Seats ${booking.seatLabels} will be released.`}
			</ConfirmDialog>
		</div>
	);
};

export default TicketPage;
