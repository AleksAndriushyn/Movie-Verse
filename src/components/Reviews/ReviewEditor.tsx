import React, { useState } from "react";
import { useLocation } from "react-router-dom";
import { ApiError } from "@/api/AxiosInstance";
import { getLoginUrl } from "@/api/me";
import { useCurrentUser } from "@/hooks/useAuth";
import { useOwnReview, useOwnReviewMutations } from "@/hooks/useReviews";
import type { OwnReview } from "@/types/review";
import { ConfirmDialog } from "@/components/UI/ConfirmDialog";

const MIN_LENGTH = 10;
const MAX_LENGTH = 4000;

const STATUS_NOTES: Partial<Record<OwnReview["status"], string>> = {
	hidden: "Your review was hidden after reports from other users and is waiting for a moderator. Only you can see it.",
	removed: "A moderator removed your review, so it can't be edited.",
};

const errorMessage = (error: unknown) =>
	error instanceof ApiError && error.status && error.status < 500
		? error.message
		: "Couldn't save your review. Please try again.";

type ReviewFormProps = {
	movieId: number;
	review: OwnReview | null;
	onDone: () => void;
};

const ReviewForm: React.FC<ReviewFormProps> = ({ movieId, review, onDone }) => {
	const { save } = useOwnReviewMutations(movieId);
	const [body, setBody] = useState(review?.body ?? "");
	const [containsSpoilers, setContainsSpoilers] = useState(review?.containsSpoilers ?? false);

	const tooShort = body.trim().length < MIN_LENGTH;

	const handleSubmit = (event: React.FormEvent) => {
		event.preventDefault();
		save.mutate({ body: body.trim(), containsSpoilers }, { onSuccess: onDone });
	};

	return (
		<form onSubmit={handleSubmit} className="rounded-lg bg-gray-800 p-4 space-y-3">
			<label htmlFor="review-body" className="block font-semibold text-white">
				{review ? "Edit your review" : "Write a review"}
			</label>
			<textarea
				id="review-body"
				value={body}
				onChange={(event) => setBody(event.target.value)}
				maxLength={MAX_LENGTH}
				rows={5}
				placeholder="What did you think of the movie?"
				className="w-full rounded-lg bg-gray-900 border border-gray-700 p-3 text-gray-200 focus:outline-none focus:ring-2 focus:ring-red-600"
			/>
			<div className="flex flex-wrap items-center justify-between gap-3">
				<label className="flex items-center gap-2 text-sm text-gray-300">
					<input
						type="checkbox"
						checked={containsSpoilers}
						onChange={(event) => setContainsSpoilers(event.target.checked)}
						className="accent-red-600"
					/>
					Contains spoilers
				</label>
				<span className="text-xs text-gray-500">
					{body.trim().length}/{MAX_LENGTH}
					{tooShort && ` · at least ${MIN_LENGTH} characters`}
				</span>
			</div>
			{save.isError && <p role="alert" className="text-sm text-red-400">{errorMessage(save.error)}</p>}
			<div className="flex gap-2">
				<button type="submit" disabled={tooShort || save.isPending} className="auth-button w-auto disabled:opacity-50">
					{save.isPending ? "Saving…" : "Publish"}
				</button>
				<button type="button" onClick={onDone} className="px-4 py-2 rounded-full text-gray-300 hover:text-white">
					Cancel
				</button>
			</div>
			<p className="text-xs text-gray-500">Reviews are public and show your Google name and picture.</p>
		</form>
	);
};

/** The signed-in user's own review: a prompt, the editor, or their review's status and actions. */
export const ReviewEditor: React.FC<{ movieId: number }> = ({ movieId }) => {
	const location = useLocation();
	const { data: user } = useCurrentUser();
	const { data: review, isLoading } = useOwnReview(movieId);
	const { remove } = useOwnReviewMutations(movieId);
	const [editing, setEditing] = useState(false);
	const [confirmingDelete, setConfirmingDelete] = useState(false);

	if (!user) {
		return (
			<p className="text-gray-400">
				<a href={getLoginUrl(location.pathname + location.search)} className="text-red-400 hover:text-red-300 underline">
					Sign in
				</a>{" "}
				to write a review.
			</p>
		);
	}

	if (isLoading) {
		return <div className="h-12 rounded-lg bg-gray-800 animate-pulse" />;
	}

	if (editing) {
		return <ReviewForm movieId={movieId} review={review ?? null} onDone={() => setEditing(false)} />;
	}

	if (!review) {
		return (
			<button type="button" onClick={() => setEditing(true)} className="auth-button w-auto">
				Write a review
			</button>
		);
	}

	const note = STATUS_NOTES[review.status];


	return (
		<div className="space-y-2">
			{note && <p role="status" className="rounded-lg border border-yellow-800 bg-yellow-950/50 p-3 text-sm text-yellow-200">{note}</p>}
			{review.status === "hidden" && <p className="rounded-lg bg-gray-800 p-4 text-gray-300 whitespace-pre-line break-words">{review.body}</p>}
			{review.status !== "removed" && (
				<div className="flex gap-4 text-sm">
					<button type="button" onClick={() => setEditing(true)} className="text-red-400 hover:text-red-300">
						Edit your review
					</button>
					<button type="button" onClick={() => setConfirmingDelete(true)} disabled={remove.isPending} className="text-gray-400 hover:text-white">
						Delete
					</button>
				</div>
			)}
			<ConfirmDialog
				open={confirmingDelete}
				title="Delete your review?"
				confirmLabel="Delete"
				destructive
				pending={remove.isPending}
				error={remove.isError ? "Couldn't delete the review. Please try again." : null}
				onConfirm={() => remove.mutate(undefined, { onSuccess: () => setConfirmingDelete(false) })}
				onClose={() => setConfirmingDelete(false)}
			>
				It disappears from the movie page right away.
			</ConfirmDialog>
		</div>
	);
};
