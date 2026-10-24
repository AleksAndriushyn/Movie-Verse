import React from "react";

/** Every booking page says plainly that nothing here is real. */
export const DemoBanner: React.FC = () => (
	<p role="note" className="mb-6 rounded-lg border border-yellow-800 bg-yellow-950/50 px-4 py-3 text-sm text-yellow-200">
		<strong>Demo cinemas.</strong> Flamingo Screens and Potuzhna Zrada Cinema are fictional, tickets aren't real, and
		payments run in Stripe's test mode: use card 4242 4242 4242 4242, nothing is charged.
	</p>
);
