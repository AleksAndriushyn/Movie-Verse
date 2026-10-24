import React from "react";
import clsx from "clsx";
import { Dialog, DialogPanel, DialogTitle } from "@headlessui/react";

type ConfirmDialogProps = {
	open: boolean;
	title: string;
	children: React.ReactNode;
	confirmLabel: string;
	cancelLabel?: string;
	/** Red confirm button for actions that can't be undone. */
	destructive?: boolean;
	pending?: boolean;
	error?: string | null;
	onConfirm: () => void;
	onClose: () => void;
};

/**
 * In-page confirmation instead of window.confirm, which some embedded browsers and webviews
 * silently answer with "no" without showing anything.
 */
export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
	open,
	title,
	children,
	confirmLabel,
	cancelLabel = "Keep it",
	destructive = false,
	pending = false,
	error,
	onConfirm,
	onClose,
}) => (
	<Dialog open={open} onClose={() => !pending && onClose()} className="relative z-50">
		<div className="fixed inset-0 bg-black/70" aria-hidden="true" />
		<div className="fixed inset-0 flex items-center justify-center p-4">
			<DialogPanel className="w-full max-w-md rounded-2xl bg-gray-800 p-6 shadow-2xl">
				<DialogTitle className="text-lg font-bold text-white">{title}</DialogTitle>
				<div className="mt-2 text-sm text-gray-300">{children}</div>
				{error && <p role="alert" className="mt-3 text-sm text-red-400">{error}</p>}
				<div className="mt-6 flex justify-end gap-2">
					<button
						type="button"
						onClick={onClose}
						disabled={pending}
						className="rounded-full px-4 py-2 text-sm font-semibold text-gray-300 hover:text-white disabled:opacity-50"
					>
						{cancelLabel}
					</button>
					<button
						type="button"
						onClick={onConfirm}
						disabled={pending}
						className={clsx(
							"rounded-full px-4 py-2 text-sm font-semibold text-white disabled:opacity-50",
							destructive ? "bg-red-600 hover:bg-red-700" : "bg-gray-600 hover:bg-gray-500"
						)}
					>
						{pending ? "Working…" : confirmLabel}
					</button>
				</div>
			</DialogPanel>
		</div>
	</Dialog>
);
