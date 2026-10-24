export interface Cinema {
  id: number;
  name: string;
  address: string;
  /** IANA zone the cinema's times are shown in (San Diego). */
  timeZone: string;
}

export interface ShowtimeSlot {
  id: number;
  cinemaId: number;
  hallName: string;
  startsAt: string;
  bookable: boolean;
}

export interface MovieSchedule {
  movieId: number;
  title: string;
  posterPath: string | null;
  showtimes: ShowtimeSlot[];
}

export interface DaySchedule {
  /** Cinema-local date, "YYYY-MM-DD". */
  date: string;
  dates: string[];
  cinemas: Cinema[];
  movies: MovieSchedule[];
}

export interface MovieShowtime extends ShowtimeSlot {
  date: string;
}

export interface MovieShowtimes {
  cinemas: Cinema[];
  showtimes: MovieShowtime[];
}

export type SeatType = 'standard' | 'vip' | 'wheelchair';
export type SeatStatus = 'free' | 'taken' | 'mine';

export interface Seat {
  id: number;
  number: number;
  column: number;
  type: SeatType;
  status: SeatStatus;
}

export interface SeatRow {
  row: string;
  seats: Seat[];
}

export interface ShowtimeDetails {
  id: number;
  movieId: number;
  movieTitle: string;
  posterPath: string | null;
  startsAt: string;
  cinema: Cinema;
  hallName: string;
  standardPriceCents: number;
  vipPriceCents: number;
  currency: string;
  bookable: boolean;
  rows: SeatRow[];
  myHoldBookingId: string | null;
  myHoldExpiresAt: string | null;
}

export type BookingStatus = 'pending' | 'paid' | 'cancelled' | 'expired' | 'refunded';

export interface Booking {
  id: string;
  status: BookingStatus;
  showtimeId: number;
  movieId: number;
  movieTitle: string;
  posterPath: string | null;
  cinema: Cinema;
  hallName: string;
  startsAt: string;
  seatLabels: string;
  totalCents: number;
  currency: string;
  ticketCode: string | null;
  holdExpiresAt: string | null;
  checkoutUrl: string | null;
  canCancel: boolean;
}
