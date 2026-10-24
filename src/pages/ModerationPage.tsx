import React, { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import Pagination from "@/components/Pagination/Pagination";
import { Avatar } from "@/components/Reviews/Avatar";
import { formatReviewDate } from "@/components/Reviews/format";
import { ConfirmDialog } from "@/components/UI/ConfirmDialog";
import { useCurrentUser } from "@/hooks/useAuth";
import { useModerateReview, useModerationQueue } from "@/hooks/useReviews";
import type { ModeratedReview, ReportReason } from "@/types/review";
import getPageParam from "@/utils/page-param";
import NotFoundPage from "./NotFoundPage";

const REASON_LABELS: Record<ReportReason, string> = {
	spam: "spam",
	offensive: "offensive",
	spoilers: "unmarked spoilers",
	other: "other",
};

const QueueItem: React.FC<{ review: ModeratedReview }> = ({ review }) => {
	const moderate = useModerateReview();
	const [confirmingRemove, setConfirmingRemove] = useState(false);

	const reasons = Object.entries(review.reasons)
		.map(([reason, count]) => `${REASON_LABELS[reason as ReportReason]} ×${count}`)
		.join(", ");

	return (
		<article className="rounded-lg bg-gray-800 p-4">
			<header className="flex flex-wrap items-center gap-3 mb-3">
				<Avatar name={review.author.name} url={review.author.avatarUrl} />
				<div className="min-w-0 flex-1">
					<p className="font-semibold text-white truncate">
						{review.author.name ?? "MovieVerse user"}
						{review.authorEmail && <span className="ml-2 text-xs font-normal text-gray-400">{review.authorEmail}</span>}
					</p>
					<p className="text-xs text-gray-400">
						on{" "}
						<Link to={`/movie/${review.movieId}`} className="text-red-400 hover:text-red-300">
							{review.movieTitle ?? `movie #${review.movieId}`}
						</Link>{" "}
						· {formatReviewDate(review.createdAt)}
					</p>
				</div>
				<span
					className={
						review.status === "hidden"
							? "rounded-full bg-yellow-900/70 px-2 py-0.5 text-xs text-yellow-200"
							: "rounded-full bg-gray-700 px-2 py-0.5 text-xs text-gray-300"
					}
				>
					{review.status === "hidden" ? "Hidden" : "Visible"}
				</span>
			</header>
			<p className="text-sm text-gray-400 mb-2">
				{review.reportCount} {review.reportCount === 1 ? "report" : "reports"}: {reasons}
				{review.containsSpoilers && " · marked as spoilers"}
			</p>
			<p className="text-gray-300 whitespace-pre-line break-words">{review.body}</p>
			<footer className="mt-4 flex gap-2">
				<button
					type="button"
					onClick={() => moderate.mutate({ reviewId: review.id, action: "restore" })}
					disabled={moderate.isPending}
					className="px-4 py-2 rounded-full bg-gray-700 hover:bg-gray-600 text-sm font-semibold disabled:opacity-50"
				>
					Keep and clear reports
				</button>
				<button
					type="button"
					onClick={() => setConfirmingRemove(true)}
					disabled={moderate.isPending}
					className="px-4 py-2 rounded-full bg-red-700 hover:bg-red-600 text-sm font-semibold disabled:opacity-50"
				>
					Remove
				</button>
			</footer>
			{moderate.isError && !confirmingRemove && (
				<p role="alert" className="mt-2 text-sm text-red-400">Couldn't apply that. Please try again.</p>
			)}
			<ConfirmDialog
				open={confirmingRemove}
				title="Remove this review for good?"
				confirmLabel="Remove"
				destructive
				pending={moderate.isPending}
				error={moderate.isError ? "Couldn't remove the review. Please try again." : null}
				onConfirm={() =>
					moderate.mutate({ reviewId: review.id, action: "remove" }, { onSuccess: () => setConfirmingRemove(false) })
				}
				onClose={() => setConfirmingRemove(false)}
			>
				The author won't be able to edit or re-post it.
			</ConfirmDialog>
		</article>
	);
};

const ModerationPage: React.FC = () => {
	const [searchParams] = useSearchParams();
	const page = getPageParam(searchParams);
	const { data: user, isLoading: isUserLoading } = useCurrentUser();
	const { data, isLoading, isError } = useModerationQueue(page, !!user?.isAdmin);

	if (isUserLoading) {
		return null;
	}

	// The API enforces admin rights; non-admins just don't learn the page exists
	if (!user?.isAdmin) {
		return <NotFoundPage />;
	}

	return (
		<div className="max-w-4xl mx-auto px-4 py-8">
			<title>Moderation | MovieVerse</title>
			<h1 className="text-3xl font-bold text-white mb-2">Review moderation</h1>
			<p className="text-gray-400 mb-8">
				Reviews hide automatically after 3 reports. Keep a review to clear its reports, or remove it for good.
			</p>
			{isLoading && <div className="h-32 rounded-lg bg-gray-800 animate-pulse" />}
			{isError && <p className="text-red-400">Couldn't load the queue. Please try again later.</p>}
			{data && data.results.length === 0 && <p className="text-gray-400">Nothing to review. All clear.</p>}
			<div className="space-y-4">
				{data?.results.map((review) => (
					<QueueItem key={review.id} review={review} />
				))}
			</div>
			{data && (
				<div className="flex justify-center mt-8">
					<Pagination totalPages={data.totalPages} isFetching={false} />
				</div>
			)}
		</div>
	);
};

export default ModerationPage;
