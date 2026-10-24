import React, { useState } from "react";

const STORAGE_KEY = "mv:demoBannerDismissedAt";

/** A dismissed banner comes back after this long, in case the visitor forgot it's a demo. */
const SHOW_AGAIN_AFTER_MS = 3 * 24 * 60 * 60 * 1000;

// Storage can be unavailable (private mode, blocked site data); then the banner just shows
const wasDismissedRecently = () => {
	try {
		const dismissedAt = Number(localStorage.getItem(STORAGE_KEY));
		return dismissedAt > 0 && Date.now() - dismissedAt < SHOW_AGAIN_AFTER_MS;
	} catch {
		return false;
	}
};

/** Booking pages say plainly that nothing here is real; the visitor can hide it for a few days. */
export const DemoBanner: React.FC = () => {
	const [hidden, setHidden] = useState(wasDismissedRecently);

	if (hidden) {
		return null;
	}

	const dismiss = () => {
		try {
			localStorage.setItem(STORAGE_KEY, String(Date.now()));
		} catch {
			// hide for this page view at least
		}
		setHidden(true);
	};

	return (
		<div role="note" className="mb-6 flex items-start gap-3 rounded-lg border border-yellow-800 bg-yellow-950/50 px-4 py-3 text-sm text-yellow-200">
			<p className="flex-1">
				<strong>Demo cinemas.</strong> Flamingo Screens and Potuzhniy Cinema are fictional, tickets aren't real, and
				payments run in Stripe's test mode: use card 4242 4242 4242 4242, nothing is charged.
			</p>
			<button
				type="button"
				onClick={dismiss}
				aria-label="Hide this notice"
				title="Hide for 3 days"
				className="-m-1 shrink-0 rounded p-1 text-yellow-200/70 hover:bg-yellow-900/50 hover:text-yellow-100"
			>
				<svg viewBox="0 0 20 20" className="h-4 w-4" fill="currentColor" aria-hidden="true">
					<path d="M6.28 5.22a.75.75 0 00-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 101.06 1.06L10 11.06l3.72 3.72a.75.75 0 101.06-1.06L11.06 10l3.72-3.72a.75.75 0 00-1.06-1.06L10 8.94 6.28 5.22z" />
				</svg>
			</button>
		</div>
	);
};
