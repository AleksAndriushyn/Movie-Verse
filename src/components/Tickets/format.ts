/** Showtime clock time in the cinema's zone, e.g. "7:30 PM". */
export const formatShowTime = (iso: string, timeZone: string) =>
  new Intl.DateTimeFormat('en-US', { hour: 'numeric', minute: '2-digit', timeZone }).format(new Date(iso));

/** e.g. "Sat, Oct 25". */
export const formatShowDate = (iso: string, timeZone: string) =>
  new Intl.DateTimeFormat('en-US', { weekday: 'short', month: 'short', day: 'numeric', timeZone }).format(new Date(iso));

/** A cinema-local "YYYY-MM-DD" date as a chip label; parsed as UTC so the viewer's zone can't shift the day. */
export const formatDateLabel = (date: string) =>
  new Intl.DateTimeFormat('en-US', { weekday: 'short', month: 'short', day: 'numeric', timeZone: 'UTC' }).format(
    new Date(`${date}T00:00:00Z`)
  );

export const formatMoney = (cents: number, currency: string) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: currency.toUpperCase() }).format(cents / 100);
