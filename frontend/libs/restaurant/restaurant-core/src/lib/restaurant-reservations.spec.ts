import { describe, expect, it } from 'vitest';
import {
  Reservation,
  ReservationDraft,
  WaitingParty,
  blankBooking,
  bookingProblem,
  bookingRequest,
  clashingBooking,
  endsAt,
  minutesWaited,
  nextBookingFor,
  nextThatFits,
  overlaps,
  stillToArrive,
  timeOf,
} from './restaurant-reservations';

function booking(id: number, at: string, table: number | null = 1, status: Reservation['status'] = 'Booked', minutes = 90): Reservation {
  return {
    tableReservationId: id,
    guestName: `Guest ${id}`,
    phone: '9876543210',
    partySize: 2,
    reservedFor: at,
    durationMinutes: minutes,
    restaurantTableId: table,
    tableCode: table ? `T${table}` : null,
    status,
    restaurantOrderId: null,
    seatedAt: null,
    notes: null,
  };
}

function draft(over: Partial<ReservationDraft> = {}): ReservationDraft {
  return { guestName: 'Ravi', phone: '98765 43210', partySize: 4, reservedFor: '2026-10-10T20:30', durationMinutes: 90, restaurantTableId: 1, notes: '', ...over };
}

describe('bookings (TK-165)', () => {
  it('a table booked for 8 pm clashes with a booking at 8:30 pm', () => {
    expect(overlaps('2026-10-10T20:00', 90, '2026-10-10T20:30', 90)).toBe(true);
    expect(clashingBooking(draft(), [booking(1, '2026-10-10T20:00')], null)?.tableReservationId).toBe(1);
  });

  it('one booking ending as another starts does not clash', () => {
    expect(overlaps('2026-10-10T18:30', 90, '2026-10-10T20:00', 90)).toBe(false);
    expect(clashingBooking(draft({ reservedFor: '2026-10-10T21:30' }), [booking(1, '2026-10-10T20:00')], null)).toBeNull();
  });

  it('a booking never clashes with itself, another table, or one no longer held', () => {
    const existing = [booking(1, '2026-10-10T20:00'), booking(2, '2026-10-10T20:00', 2), booking(3, '2026-10-10T20:00', 1, 'Cancelled')];
    expect(clashingBooking(draft(), existing.slice(1), null)).toBeNull();
    expect(clashingBooking(draft(), existing, 1)).toBeNull();
  });

  it('reads the times a booking is shown with', () => {
    expect(timeOf('2026-10-10T20:00:00')).toBe('20:00');
    expect(endsAt(booking(1, '2026-10-10T23:00'))).toBe('00:30');
  });

  it('says what is missing before sending', () => {
    expect(bookingProblem(draft())).toBeNull();
    expect(bookingProblem(draft({ guestName: ' ' }))).toBe("Enter the guest's name.");
    expect(bookingProblem(draft({ phone: '' }))).toBe('Enter a phone number to reach the guest.');
    expect(bookingProblem(draft({ partySize: 0 }))).toContain('between 1 and 500');
    expect(bookingProblem(draft({ reservedFor: '' }))).toContain('date and time');
    expect(bookingProblem(draft({ durationMinutes: 10 }))).toContain('15 minutes');
  });

  it('sends a trimmed request with the default hold', () => {
    expect(bookingRequest(draft({ guestName: ' Ravi ', durationMinutes: null, notes: ' ' }))).toMatchObject({
      guestName: 'Ravi',
      durationMinutes: 90,
      notes: null,
    });
  });

  it('starts a new booking at the next half hour', () => {
    expect(blankBooking('2026-10-10T19:10').reservedFor).toBe('2026-10-10T19:30');
    expect(blankBooking('2026-10-10T19:30').reservedFor).toBe('2026-10-10T20:00');
  });

  it("shows today's bookings still to arrive on the floor plan, earliest first", () => {
    const list = [booking(1, '2026-10-10T21:00'), booking(2, '2026-10-10T19:45', 2), booking(3, '2026-10-10T18:00', 3), booking(4, '2026-10-10T20:00', 4, 'Seated')];
    expect(stillToArrive(list, '2026-10-10T20:00').map((r) => r.tableReservationId)).toEqual([2, 1]);
    expect(nextBookingFor(1, list, '2026-10-10T20:00')?.tableReservationId).toBe(1);
    expect(nextBookingFor(3, list, '2026-10-10T20:00')).toBeNull();
  });
});

describe('waiting list (TK-165)', () => {
  const party = (id: number, size: number, joined: string, status: WaitingParty['status'] = 'Waiting'): WaitingParty => ({
    waitingListEntryId: id,
    guestName: `Party ${id}`,
    phone: null,
    partySize: size,
    joinedAt: joined,
    status,
    restaurantTableId: null,
    tableCode: null,
    restaurantOrderId: null,
    closedAt: null,
    notes: null,
  });

  it('a free table takes the party waiting longest that fits it', () => {
    const waiting = [party(1, 6, '2026-10-10T19:00'), party(2, 2, '2026-10-10T19:10'), party(3, 4, '2026-10-10T19:05', 'Left'), party(4, 3, '2026-10-10T19:20')];
    expect(nextThatFits(waiting, 4)?.waitingListEntryId).toBe(2);
    expect(nextThatFits(waiting, 1)).toBeNull();
  });

  it('counts the minutes waited', () => {
    expect(minutesWaited('2026-10-10T19:10', '2026-10-10T19:55')).toBe(45);
    expect(minutesWaited('2026-10-10T19:10', '2026-10-10T19:00')).toBe(0);
  });
});
