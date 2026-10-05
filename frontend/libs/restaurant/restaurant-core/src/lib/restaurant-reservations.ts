/**
 * Table bookings and the waiting list (TK-165): the shapes Sales's
 * `api/restaurant/reservations` and `api/restaurant/waiting-list` return,
 * and the pure rules the bookings page and the floor plan's strip read.
 * `overlaps` and `nextThatFits` mirror `TableReservationRulesRepository`;
 * the server decides, this only lets the page say so before asking.
 *
 * Times are branch wall-clock, `yyyy-MM-ddTHH:mm[:ss]` with no offset, as
 * `bb-datetime-input` holds them.
 */

export type ReservationStatus = 'Booked' | 'Seated' | 'NoShow' | 'Cancelled';

export type WaitingStatus = 'Waiting' | 'Seated' | 'Left';

export interface Reservation {
  tableReservationId: number;
  guestName: string;
  phone: string;
  partySize: number;
  reservedFor: string;
  durationMinutes: number;
  restaurantTableId: number | null;
  tableCode: string | null;
  status: ReservationStatus;
  restaurantOrderId: number | null;
  seatedAt: string | null;
  notes: string | null;
}

export interface ReservationDraft {
  guestName: string;
  phone: string;
  partySize: number | null;
  reservedFor: string;
  durationMinutes: number | null;
  restaurantTableId: number | null;
  notes: string;
}

export interface WaitingParty {
  waitingListEntryId: number;
  guestName: string;
  phone: string | null;
  partySize: number;
  joinedAt: string;
  status: WaitingStatus;
  restaurantTableId: number | null;
  tableCode: string | null;
  restaurantOrderId: number | null;
  closedAt: string | null;
  notes: string | null;
}

export interface SeatedParty {
  restaurantOrderId: number;
  restaurantTableId: number;
}

/** How long a booking holds its table unless the host says otherwise. */
export const DEFAULT_BOOKING_MINUTES = 90;

/** A wall-clock `yyyy-MM-ddTHH:mm` as minutes, read as UTC so no time zone shifts it; NaN when it is not one. */
export function wallClockMinutes(value: string): number {
  const m = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/.exec(value ?? '');
  if (!m) return Number.NaN;
  return Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5]) / 60000;
}

/** True when two bookings for one table overlap; one ending as the other starts does not. */
export function overlaps(start: string, minutes: number, otherStart: string, otherMinutes: number): boolean {
  const a = wallClockMinutes(start);
  const b = wallClockMinutes(otherStart);
  return a < b + otherMinutes && b < a + minutes;
}

/** `HH:mm` of a wall-clock time. */
export function timeOf(value: string): string {
  const m = /[T ](\d{2}:\d{2})/.exec(value ?? '');
  return m ? m[1] : '';
}

/** The `yyyy-MM-dd` of a wall-clock time. */
export function dateOf(value: string): string {
  return (value ?? '').slice(0, 10);
}

/** When a booking's hold ends, `HH:mm`. */
export function endsAt(reservation: Pick<Reservation, 'reservedFor' | 'durationMinutes'>): string {
  const start = wallClockMinutes(reservation.reservedFor);
  if (Number.isNaN(start)) return '';
  const end = new Date((start + reservation.durationMinutes) * 60000);
  return `${String(end.getUTCHours()).padStart(2, '0')}:${String(end.getUTCMinutes()).padStart(2, '0')}`;
}

/** The booking on the same table that clashes with a draft, if the page already has it. */
export function clashingBooking(draft: ReservationDraft, existing: readonly Reservation[], editingId: number | null): Reservation | null {
  if (draft.restaurantTableId == null || !draft.reservedFor) return null;
  const minutes = draft.durationMinutes ?? DEFAULT_BOOKING_MINUTES;
  return (
    existing.find(
      (r) =>
        r.tableReservationId !== editingId &&
        r.restaurantTableId === draft.restaurantTableId &&
        (r.status === 'Booked' || r.status === 'Seated') &&
        overlaps(draft.reservedFor, minutes, r.reservedFor, r.durationMinutes),
    ) ?? null
  );
}

/** What is wrong with a booking before it is sent, or null. */
export function bookingProblem(draft: ReservationDraft): string | null {
  if (!draft.guestName.trim()) return "Enter the guest's name.";
  if (!draft.phone.trim()) return 'Enter a phone number to reach the guest.';
  if (!draft.partySize || draft.partySize < 1 || draft.partySize > 500) return 'A party is between 1 and 500 guests.';
  if (Number.isNaN(wallClockMinutes(draft.reservedFor))) return 'Choose the date and time they are coming.';
  const minutes = draft.durationMinutes ?? DEFAULT_BOOKING_MINUTES;
  if (minutes < 15 || minutes > 720) return 'A booking holds a table for between 15 minutes and 12 hours.';
  return null;
}

/** The request a draft becomes. */
export function bookingRequest(draft: ReservationDraft) {
  return {
    guestName: draft.guestName.trim(),
    phone: draft.phone.trim(),
    partySize: draft.partySize,
    reservedFor: draft.reservedFor,
    durationMinutes: draft.durationMinutes ?? DEFAULT_BOOKING_MINUTES,
    restaurantTableId: draft.restaurantTableId,
    notes: draft.notes.trim() || null,
  };
}

/** Today's bookings still to arrive, from now on (and those up to 30 minutes late), earliest first: the floor plan's strip. */
export function stillToArrive(reservations: readonly Reservation[], now: string): Reservation[] {
  const cutoff = wallClockMinutes(now) - 30;
  return reservations
    .filter((r) => r.status === 'Booked' && wallClockMinutes(r.reservedFor) >= cutoff)
    .sort((a, b) => wallClockMinutes(a.reservedFor) - wallClockMinutes(b.reservedFor));
}

/** The next booking held on a table today, for its tile. */
export function nextBookingFor(tableId: number, reservations: readonly Reservation[], now: string): Reservation | null {
  return stillToArrive(reservations, now).find((r) => r.restaurantTableId === tableId) ?? null;
}

/** The party waiting longest that fits a table seating `seats`. */
export function nextThatFits(waiting: readonly WaitingParty[], seats: number): WaitingParty | null {
  return (
    waiting
      .filter((w) => w.status === 'Waiting' && w.partySize <= seats)
      .sort((a, b) => wallClockMinutes(a.joinedAt) - wallClockMinutes(b.joinedAt))[0] ?? null
  );
}

/** Whole minutes a party has waited. */
export function minutesWaited(joinedAt: string, now: string): number {
  const waited = Math.floor(wallClockMinutes(now) - wallClockMinutes(joinedAt));
  return Number.isNaN(waited) || waited < 0 ? 0 : waited;
}

/** The branch's wall clock now, as `yyyy-MM-ddTHH:mm`. */
export function wallClockNow(at: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}T${pad(at.getHours())}:${pad(at.getMinutes())}`;
}

export function reservationStatusLabel(status: ReservationStatus): string {
  return status === 'NoShow' ? 'No-show' : status;
}

/** A new booking's draft: today, the next half hour, two guests, no table yet. */
export function blankBooking(now: string): ReservationDraft {
  const minutes = wallClockMinutes(now);
  const next = Number.isNaN(minutes) ? Number.NaN : Math.ceil((minutes + 1) / 30) * 30;
  const at = Number.isNaN(next) ? '' : new Date(next * 60000).toISOString().slice(0, 16);
  return { guestName: '', phone: '', partySize: 2, reservedFor: at, durationMinutes: DEFAULT_BOOKING_MINUTES, restaurantTableId: null, notes: '' };
}
