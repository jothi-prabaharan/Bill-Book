import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { IfCanDirective } from '@bill-book/auth';
import {
  FloorPlan,
  RESTAURANT_API,
  Reservation,
  ReservationDraft,
  RestaurantTable,
  SeatedParty,
  WaitingParty,
  allTables,
  blankBooking,
  bookingProblem,
  bookingRequest,
  clashingBooking,
  dateOf,
  endsAt,
  minutesWaited,
  reservationStatusLabel,
  timeOf,
  wallClockNow,
} from '@bill-book/restaurant-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ConfirmDialogService,
  DateTimeInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
  onCreateRequest,
} from '@bill-book/ui-components';

type Sheet = 'booking' | 'seat-booking' | 'waiting' | 'seat-waiting' | null;

/**
 * Restaurant › Bookings (TK-165): one day's table bookings beside the
 * waiting list. A booking holds a table for a time; seating it, or a waiting
 * party, opens an order on the table and goes to it. Shown only while tables
 * are switched on, as the bookings hold tables.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-bookings',
  standalone: true,
  imports: [
    FormsModule,
    IfCanDirective,
    BranchDateInputComponent,
    DateTimeInputComponent,
    MessageBoxComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
  ],
  templateUrl: './restaurant-bookings.page.html',
  styleUrl: './restaurant-bookings.page.scss',
})
export class RestaurantBookingsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly day = signal(dateOf(wallClockNow()));
  protected readonly bookings = signal<Reservation[]>([]);
  protected readonly waiting = signal<WaitingParty[]>([]);
  protected readonly tables = signal<RestaurantTable[]>([]);
  protected readonly now = signal(wallClockNow());
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly sheet = signal<Sheet>(null);

  protected editingId: number | null = null;
  protected draft: ReservationDraft = blankBooking(wallClockNow());
  protected seatingBooking: Reservation | null = null;
  protected seatingParty: WaitingParty | null = null;
  protected seatTableId: number | null = null;
  protected waitingDraft = { guestName: '', phone: '', partySize: 2 as number | null, notes: '' };

  protected readonly timeOf = timeOf;
  protected readonly endsAt = endsAt;
  protected readonly statusLabel = reservationStatusLabel;

  protected readonly tableOptions = computed<BbSelectOption<number>[]>(() =>
    this.tables().map((t) => ({ value: t.restaurantTableId, label: `${t.code} · ${t.seats} seats` })),
  );

  /** Tables a party can be seated at now: free, and big enough. */
  protected seatOptions(partySize: number): BbSelectOption<number>[] {
    return this.tables()
      .filter((t) => t.status === 'Free' && t.seats >= partySize)
      .map((t) => ({ value: t.restaurantTableId, label: `${t.code} · ${t.seats} seats` }));
  }

  constructor() {
    // The secondary menu's + asks for a new booking with ?action=create (TK-189).
    onCreateRequest(() => this.newBooking());
  }

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.now.set(wallClockNow());
    try {
      const [bookings, waiting, plan] = await Promise.all([
        firstValueFrom(this.http.get<Reservation[]>(`${RESTAURANT_API}/reservations?date=${this.day()}`)),
        firstValueFrom(this.http.get<WaitingParty[]>(`${RESTAURANT_API}/waiting-list`)),
        firstValueFrom(this.http.get<FloorPlan>(`${RESTAURANT_API}/floor-plan`)),
      ]);
      this.bookings.set(bookings);
      this.waiting.set(waiting);
      this.tables.set(allTables(plan).filter((t) => t.isActive && t.mergedIntoTableId == null));
    } catch (error) {
      this.fail(error, 'The bookings could not be loaded.');
    }
  }

  protected changeDay(day: string | null): void {
    if (!day) return;
    this.day.set(day);
    void this.load();
  }

  protected waited(party: WaitingParty): number {
    return minutesWaited(party.joinedAt, this.now());
  }

  // ---- Bookings ----

  protected newBooking(): void {
    this.editingId = null;
    const draft = blankBooking(wallClockNow());
    if (this.day() !== dateOf(draft.reservedFor)) {
      draft.reservedFor = `${this.day()}T19:00`;
    }
    this.draft = draft;
    this.openSheet('booking');
  }

  protected editBooking(booking: Reservation): void {
    this.editingId = booking.tableReservationId;
    this.draft = {
      guestName: booking.guestName,
      phone: booking.phone,
      partySize: booking.partySize,
      reservedFor: booking.reservedFor.slice(0, 16),
      durationMinutes: booking.durationMinutes,
      restaurantTableId: booking.restaurantTableId,
      notes: booking.notes ?? '',
    };
    this.openSheet('booking');
  }

  protected async saveBooking(): Promise<void> {
    const problem = bookingProblem(this.draft);
    const clash = problem ? null : clashingBooking(this.draft, this.bookings(), this.editingId);
    if (problem || clash) {
      this.messages.set([{ tone: 'error', text: problem ?? `Table ${clash!.tableCode} is booked from ${timeOf(clash!.reservedFor)} to ${endsAt(clash!)}.` }]);
      return;
    }
    const body = bookingRequest(this.draft);
    const id = this.editingId;
    await this.run(
      () =>
        id == null
          ? firstValueFrom(this.http.post(`${RESTAURANT_API}/reservations`, body))
          : firstValueFrom(this.http.put(`${RESTAURANT_API}/reservations/${id}`, body)),
      'Booking saved.',
    );
  }

  protected async cancel(booking: Reservation): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Cancel booking?',
      message: `Cancel ${booking.guestName}'s booking?`,
      confirmLabel: 'Cancel booking',
      tone: 'danger',
    });
    if (!ok) return;
    await this.run(() => firstValueFrom(this.http.post(`${RESTAURANT_API}/reservations/${booking.tableReservationId}/cancel`, {})), 'Booking cancelled.');
  }

  protected async noShow(booking: Reservation): Promise<void> {
    await this.run(() => firstValueFrom(this.http.post(`${RESTAURANT_API}/reservations/${booking.tableReservationId}/no-show`, {})), 'Marked a no-show.');
  }

  protected startSeatBooking(booking: Reservation): void {
    this.seatingBooking = booking;
    this.seatTableId = booking.restaurantTableId;
    this.openSheet('seat-booking');
  }

  protected async seatBooking(): Promise<void> {
    const booking = this.seatingBooking;
    if (!booking) return;
    if (this.seatTableId == null) {
      this.messages.set([{ tone: 'error', text: 'Choose the table to seat them at.' }]);
      return;
    }
    await this.seat(`${RESTAURANT_API}/reservations/${booking.tableReservationId}/seat`);
  }

  // ---- Waiting list ----

  protected newWaiting(): void {
    this.waitingDraft = { guestName: '', phone: '', partySize: 2, notes: '' };
    this.openSheet('waiting');
  }

  protected async addWaiting(): Promise<void> {
    const d = this.waitingDraft;
    const problem = !d.guestName.trim() ? "Enter the guest's name." : !d.partySize || d.partySize < 1 || d.partySize > 500 ? 'A party is between 1 and 500 guests.' : null;
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }
    const body = { guestName: d.guestName.trim(), phone: d.phone.trim() || null, partySize: d.partySize, notes: d.notes.trim() || null };
    await this.run(() => firstValueFrom(this.http.post(`${RESTAURANT_API}/waiting-list`, body)), `${body.guestName} is on the waiting list.`);
  }

  protected startSeatWaiting(party: WaitingParty): void {
    this.seatingParty = party;
    this.seatTableId = this.seatOptions(party.partySize)[0]?.value ?? null;
    this.openSheet('seat-waiting');
  }

  protected async seatWaiting(): Promise<void> {
    const party = this.seatingParty;
    if (!party) return;
    if (this.seatTableId == null) {
      this.messages.set([{ tone: 'error', text: 'Choose the table to seat them at.' }]);
      return;
    }
    await this.seat(`${RESTAURANT_API}/waiting-list/${party.waitingListEntryId}/seat`);
  }

  protected async left(party: WaitingParty): Promise<void> {
    await this.run(() => firstValueFrom(this.http.post(`${RESTAURANT_API}/waiting-list/${party.waitingListEntryId}/leave`, {})), `${party.guestName} left the list.`);
  }

  // ---- Shared ----

  protected openSheet(sheet: Sheet): void {
    this.messages.set([]);
    this.sheet.set(sheet);
  }

  protected close(): void {
    this.sheet.set(null);
    this.seatingBooking = null;
    this.seatingParty = null;
  }

  /** Seats a party and goes to the order opened for them. */
  private async seat(url: string): Promise<void> {
    this.busy.set(true);
    try {
      const seated = await firstValueFrom(this.http.post<SeatedParty>(url, { restaurantTableId: this.seatTableId }));
      this.close();
      await this.router.navigate(['/restaurant/orders'], { queryParams: { order: seated.restaurantOrderId } });
    } catch (error) {
      this.fail(error, 'They could not be seated.');
    } finally {
      this.busy.set(false);
    }
  }

  private async run(work: () => Promise<unknown>, done: string): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      this.messages.set([{ tone: 'success', text: done }]);
      this.close();
      await this.load();
    } catch (error) {
      this.fail(error, 'That could not be done.');
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
