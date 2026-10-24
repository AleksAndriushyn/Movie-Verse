import React, { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { Menu, MenuButton, MenuItem, MenuItems } from "@headlessui/react";
import { getLoginUrl } from "@/api/me";
import { useCurrentUser, useDeleteAccount, useLogout } from "@/hooks/useAuth";
import { ConfirmDialog } from "@/components/UI/ConfirmDialog";

const MENU_ITEM_CLASS = "block w-full text-left px-4 py-2 text-sm text-gray-200 data-[focus]:bg-gray-700";

const AuthControls: React.FC = () => {
	const location = useLocation();
	const { data: user, isLoading } = useCurrentUser();
	const navigate = useNavigate();
	const logout = useLogout();
	const deleteAccount = useDeleteAccount();
	const [confirmingDelete, setConfirmingDelete] = useState(false);

	if (isLoading) {
		// keeps the header from shifting while the session loads
		return <div className="w-9 h-9 rounded-full bg-gray-800 animate-pulse" />;
	}

	if (!user) {
		return (
			<a href={getLoginUrl(location.pathname + location.search)} className="auth-button text-center whitespace-nowrap">
				Sign in
			</a>
		);
	}

	const displayName = user.name ?? user.email ?? "Account";

	return (
		<>
			<Menu as="div" className="relative">
				<MenuButton className="flex rounded-full focus:outline-none focus:ring-2 focus:ring-red-600" aria-label="Account menu">
					{user.avatarUrl ? (
						<img src={user.avatarUrl} alt="" referrerPolicy="no-referrer" className="w-9 h-9 rounded-full object-cover" />
					) : (
						<span className="w-9 h-9 rounded-full bg-red-600 flex items-center justify-center font-bold">
							{displayName[0].toUpperCase()}
						</span>
					)}
				</MenuButton>
				<MenuItems anchor="bottom end" className="z-30 mt-2 w-56 rounded-lg bg-gray-800 shadow-xl border border-gray-700 py-1 focus:outline-none">
					<div className="px-4 py-2 border-b border-gray-700">
						<p className="text-sm font-semibold text-white truncate">{displayName}</p>
						{user.email && <p className="text-xs text-gray-400 truncate">{user.email}</p>}
					</div>
					<MenuItem>
						<Link to="/tickets" className={MENU_ITEM_CLASS}>My tickets</Link>
					</MenuItem>
					<MenuItem>
						<Link to="/favorites" className={MENU_ITEM_CLASS}>Favorites</Link>
					</MenuItem>
					<MenuItem>
						<Link to="/watchlist" className={MENU_ITEM_CLASS}>Watchlist</Link>
					</MenuItem>
					<MenuItem>
						<Link to="/ratings" className={MENU_ITEM_CLASS}>Ratings</Link>
					</MenuItem>
					{user.isAdmin && (
						<MenuItem>
							<Link to="/admin/reviews" className={MENU_ITEM_CLASS}>Moderation</Link>
						</MenuItem>
					)}
					<MenuItem>
						<button type="button" onClick={() => logout.mutate()} className={MENU_ITEM_CLASS}>
							Sign out
						</button>
					</MenuItem>
					<div className="my-1 border-t border-gray-700" />
					<MenuItem>
						<button
							type="button"
							onClick={() => setConfirmingDelete(true)}
							disabled={deleteAccount.isPending}
							className={`${MENU_ITEM_CLASS} text-red-400!`}
						>
							Delete account
						</button>
					</MenuItem>
				</MenuItems>
			</Menu>
			<ConfirmDialog
				open={confirmingDelete}
				title="Delete your MovieVerse account?"
				confirmLabel="Delete account"
				cancelLabel="Keep my account"
				destructive
				pending={deleteAccount.isPending}
				error={deleteAccount.isError ? "Couldn't delete your account. Please try again." : null}
				onConfirm={() =>
					deleteAccount.mutate(undefined, {
						onSuccess: () => {
							setConfirmingDelete(false);
							navigate("/");
						},
					})
				}
				onClose={() => setConfirmingDelete(false)}
			>
				Your lists, ratings, reviews and bookings are deleted with it. This can't be undone.
			</ConfirmDialog>
		</>
	);
};

export default AuthControls;
