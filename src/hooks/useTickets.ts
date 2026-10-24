import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelBooking,
  createBooking,
  getMovieShowtimes,
  getMyBooking,
  getMyBookings,
  getSchedule,
  getShowtime,
  TICKET_KEYS,
} from '@/api/tickets';
import { useCurrentUser } from './useAuth';

export const useSchedule = (date: string | null) => {
  return useQuery({
    queryKey: TICKET_KEYS.schedule(date),
    queryFn: () => getSchedule(date),
    placeholderData: (previousData) => previousData,
    staleTime: 1000 * 60,
  });
};

export const useMovieShowtimes = (movieId: number) => {
  return useQuery({
    queryKey: TICKET_KEYS.movie(movieId),
    queryFn: () => getMovieShowtimes(movieId),
    staleTime: 1000 * 60,
  });
};

export const useShowtime = (showtimeId: number) => {
  return useQuery({
    queryKey: TICKET_KEYS.showtime(showtimeId),
    queryFn: () => getShowtime(showtimeId),
    enabled: showtimeId > 0,
    // seats get taken by others; keep the map reasonably fresh
    refetchInterval: 1000 * 30,
  });
};

export const useCreateBooking = (showtimeId: number) => {
  return useMutation({
    mutationFn: (seatIds: number[]) => createBooking(showtimeId, seatIds),
  });
};

export const useMyBookings = () => {
  const { data: user } = useCurrentUser();

  return useQuery({
    queryKey: TICKET_KEYS.bookings,
    queryFn: getMyBookings,
    enabled: !!user,
  });
};

/** A booking; while it's pending (e.g. right after Stripe), poll until the payment is confirmed. */
export const useMyBooking = (bookingId: string | null | undefined) => {
  const { data: user } = useCurrentUser();

  return useQuery({
    queryKey: TICKET_KEYS.booking(bookingId ?? ''),
    queryFn: () => getMyBooking(bookingId!),
    enabled: !!user && !!bookingId,
    refetchInterval: (query) => (query.state.data?.status === 'pending' ? 2000 : false),
  });
};

export const useCancelBooking = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: cancelBooking,
    onSuccess: (booking) => {
      queryClient.setQueryData(TICKET_KEYS.booking(booking.id), booking);
      queryClient.invalidateQueries({ queryKey: TICKET_KEYS.bookings, exact: true });
      queryClient.invalidateQueries({ queryKey: TICKET_KEYS.showtime(booking.showtimeId) });
    },
  });
};
