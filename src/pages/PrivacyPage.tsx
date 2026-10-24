import React from "react";

const LAST_UPDATED = "October 25, 2026";
const ISSUES_URL = "https://github.com/AleksAndriushyn/Movie-platform/issues";

const Section: React.FC<{ title: string; children: React.ReactNode }> = ({ title, children }) => (
	<section className="mb-8">
		<h2 className="text-xl font-semibold text-white mb-3">{title}</h2>
		<div className="space-y-3 text-gray-300 leading-relaxed">{children}</div>
	</section>
);

const ExternalLink: React.FC<{ href: string; children: React.ReactNode }> = ({ href, children }) => (
	<a href={href} target="_blank" rel="noopener noreferrer" className="text-red-400 hover:text-red-300 underline">
		{children}
	</a>
);

const PrivacyPage: React.FC = () => {
	return (
		<article className="max-w-3xl mx-auto px-4 py-8">
			<title>Privacy Policy | MovieVerse</title>
			<h1 className="text-3xl sm:text-4xl font-bold text-white mb-2">Privacy Policy</h1>
			<p className="text-sm text-gray-500 mb-10">Last updated: {LAST_UPDATED}</p>

			<Section title="About MovieVerse">
				<p>
					MovieVerse is a non-commercial hobby project for browsing movies. Its source code is public on{" "}
					<ExternalLink href="https://github.com/AleksAndriushyn/Movie-platform">GitHub</ExternalLink>. You can
					browse without an account; signing in is only needed to save movies.
				</p>
			</Section>

			<Section title="What we collect">
				<p>When you sign in with Google, we receive and store:</p>
				<ul className="list-disc pl-6 space-y-1">
					<li>your Google account ID, so we can recognize you next time;</li>
					<li>your name, email address and profile picture link, to show who is signed in.</li>
				</ul>
				<p>
					We never see your Google password. We only request the basic <code>openid</code>, <code>email</code>{" "}
					and <code>profile</code> permissions.
				</p>
				<p>
					We also store what you save: the movies in your favorites and watchlist, your 1-10 ratings and your
					reviews, with the time you added them. If you report a review, we store the report and its reason.
				</p>
			</Section>

			<Section title="Demo ticket bookings">
				<p>
					The cinemas on MovieVerse are fictional and so are the tickets. When you book seats we store the
					showtime, the seats, the price and the booking status. Payments run through{" "}
					<ExternalLink href="https://stripe.com/privacy">Stripe</ExternalLink> in test mode: no real money moves,
					and you should only ever enter Stripe's test card numbers. We never see card details; Stripe's checkout
					page receives them and tells us whether the (test) payment succeeded. We pass Stripe your email address
					to pre-fill the checkout page.
				</p>
			</Section>

			<Section title="Reviews are public">
				<p>
					Reviews you write are visible to everyone, together with your Google name, profile picture and your
					rating of that movie. Your email address is never shown publicly.
				</p>
				<p>
					Other users can report a review. A review reported by three people is hidden until a moderator
					looks at it; moderators can see the author's email address and can restore or remove the review.
					Reports are anonymous to the review's author.
				</p>
			</Section>

			<Section title="How we use it">
				<p>
					Only to provide the account features: signing you in, showing your lists and ratings, publishing your
					reviews and keeping them free of spam and abuse, and handling your demo ticket bookings. We do not sell or
					share your data, show ads, or build profiles. Our use of information received from Google APIs
					adheres to the{" "}
					<ExternalLink href="https://developers.google.com/terms/api-services-user-data-policy">
						Google API Services User Data Policy
					</ExternalLink>
					, including the Limited Use requirements.
				</p>
			</Section>

			<Section title="Cookies">
				<p>
					We use one essential cookie, <code>mv_auth</code>, which keeps you signed in for up to 30 days. Short-lived
					cookies protect the Google sign-in step itself. There are no analytics or advertising cookies. We
					measure page speed with Vercel Speed Insights, which collects anonymous performance metrics without
					cookies.
				</p>
			</Section>

			<Section title="Services we rely on">
				<ul className="list-disc pl-6 space-y-1">
					<li><strong>Google</strong>: sign-in.</li>
					<li>
						<strong>Microsoft Azure</strong> (EU, Poland Central): our API and the database with your account
						and lists. Server logs may contain IP addresses for security and rate limiting; IP addresses are not
						stored with your account.
					</li>
					<li><strong>Vercel</strong>: hosts the website.</li>
					<li><strong>Stripe</strong> (test mode): the checkout page for demo ticket bookings.</li>
					<li>
						<strong>TMDB</strong>: movie information. Posters and photos load from TMDB's image servers, which
						see your IP address like any website you visit.
					</li>
					<li>
						<strong>YouTube</strong>: trailer thumbnails and the trailer player. When you play a trailer,{" "}
						<ExternalLink href="https://policies.google.com/privacy">Google's privacy policy</ExternalLink>{" "}
						applies to it.
					</li>
				</ul>
			</Section>

			<Section title="Keeping and deleting your data">
				<p>
					We keep your data until you delete your account. To do that, open the account menu (your picture in
					the top right) and choose <strong>Delete account</strong>: your account and everything you saved are
					removed immediately. Copies in database backups expire within 7 days.
				</p>
				<p>
					You can also remove MovieVerse's access to your Google account at{" "}
					<ExternalLink href="https://myaccount.google.com/permissions">myaccount.google.com/permissions</ExternalLink>.
				</p>
			</Section>

			<Section title="Contact and changes">
				<p>
					Questions or requests: open an issue on{" "}
					<ExternalLink href={ISSUES_URL}>GitHub</ExternalLink>. If this policy changes, we will update the date
					at the top of this page.
				</p>
			</Section>
		</article>
	);
};

export default PrivacyPage;
