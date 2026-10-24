import { apiClient } from './AxiosInstance';
import type { Booking, DaySchedule, MovieShowtimes, ShowtimeDetails } from '@/types/tickets';

export const TICKET_KEYS = {
  schedule: (date: string | null) => ['showtimes', 'day', date],
  movie: (movieId: number) => ['showtimes', 'movie', movieId],
  showtime: (showtimeId: number) => ['showtimes', showtimeId],
  bookings: ['me', 'bookings'],
  booking: (bookingId: string) => ['me', 'bookings', bookingId],
};

export const getSchedule = (date: string | null) =>
  apiClient<DaySchedule>({ url: '/showtimes', method: 'GET', params: date ? { date } : undefined });

export const getMovieShowtimes = (movieId: number) =>
  apiClient<MovieShowtimes>({ url: `/movies/${movieId}/showtimes`, method: 'GET' });

export const getShowtime = (showtimeId: number) =>
  apiClient<ShowtimeDetails>({ url: `/showtimes/${showtimeId}`, method: 'GET' });

export const createBooking = (showtimeId: number, seatIds: number[]) =>
  apiClient<{ bookingId: string; checkoutUrl: string }>({
    url: `/showtimes/${showtimeId}/bookings`,
    method: 'POST',
    data: { seatIds },
  });

export const getMyBookings = () => apiClient<Booking[]>({ url: '/me/bookings', method: 'GET' });

export const getMyBooking = (bookingId: string) =>
  apiClient<Booking>({ url: `/me/bookings/${bookingId}`, method: 'GET' });

export const cancelBooking = (bookingId: string) =>
  apiClient<Booking>({ url: `/me/bookings/${bookingId}/cancel`, method: 'POST' });
