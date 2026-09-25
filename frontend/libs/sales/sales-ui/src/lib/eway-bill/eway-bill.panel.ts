import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import {
  EWAY_BILL_THRESHOLD,
  EwayBillCancelReason,
  ewayBillCancellable,
  ewayBillLive,
  EwayBillService,
  EwayBillView,
  EwayDocument,
  TransportMode,
} from '@bill-book/sales-core';
import {
  BbSelectOption,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';

/**
 * The e-way bill on a posted invoice or delivery challan (TK-93): its state,
 * and generating, re-vehicling and cancelling it.
 *
 * One component for both documents, because the rules are the server's and
 * are the same for both: only a posted document over the limit, one live bill
 * at a time, cancel within 24 hours. Every action needs `sales.einvoice`; the
 * server refuses without it and the refusal is shown here.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-eway-bill-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, SelectComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './eway-bill.panel.html',
  styleUrl: './eway-bill.panel.scss',
})
export class EwayBillPanel {
  private readonly service = inject(EwayBillService);

  readonly document = input.required<EwayDocument>();
  readonly documentId = input.required<number>();
  /** The consignment value, which decides whether a bill is needed at all. */
  readonly total = input<number>(0);
  readonly posted = input<boolean>(false);

  protected readonly bill = signal<EwayBillView | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly mode = signal<'view' | 'generate' | 'part-b' | 'cancel'>('view');

  protected readonly live = computed(() => ewayBillLive(this.bill()));
  protected readonly cancellable = computed(() => ewayBillCancellable(this.bill()));
  protected readonly needed = computed(() => this.total() > EWAY_BILL_THRESHOLD);
  protected readonly canGenerate = computed(() => this.posted() && this.needed() && !this.live());
  protected readonly threshold = EWAY_BILL_THRESHOLD;

  protected readonly modeOptions: BbSelectOption<string>[] = [
    { value: 'Road', label: 'Road' },
    { value: 'Rail', label: 'Rail' },
    { value: 'Air', label: 'Air' },
    { value: 'Ship', label: 'Ship' },
  ];

  protected readonly reasonOptions: BbSelectOption<string>[] = [
    { value: 'DataEntryMistake', label: 'Data entry mistake' },
    { value: 'OrderCancelled', label: 'Order cancelled' },
    { value: 'Duplicate', label: 'Duplicate' },
    { value: 'Other', label: 'Other' },
  ];

  // Part B, and the cancel form. Plain fields: the checks are the server's.
  protected transportMode = 'Road';
  protected distanceKm: number | null = null;
  protected vehicleNo = '';
  protected transporterId = '';
  protected transporterName = '';
  protected fromPlace = '';
  protected fromStateCode = '';
  protected changeReason = '';
  protected cancelReason = 'DataEntryMistake';
  protected cancelRemark = '';

  constructor() {
    effect(() => {
      const id = this.documentId();
      const document = this.document();
      if (this.posted()) {
        void this.load(document, id);
      }
    });
  }

  private async load(document: EwayDocument, id: number): Promise<void> {
    try {
      this.bill.set(await this.service.get(document, id));
    } catch (error) {
      this.error.set(readApiFailure(error).text);
    }
  }

  protected async generate(): Promise<void> {
    await this.run(() =>
      this.service.generate(this.document(), this.documentId(), {
        transportMode: this.transportMode as TransportMode,
        distanceKm: this.distanceKm ?? 0,
        vehicleNo: this.vehicleNo || null,
        transporterId: this.transporterId || null,
        transporterName: this.transporterName || null,
      }),
    );
  }

  protected async updatePartB(): Promise<void> {
    await this.run(() =>
      this.service.updatePartB(this.document(), this.documentId(), {
        vehicleNo: this.vehicleNo,
        transportMode: this.transportMode as TransportMode,
        fromPlace: this.fromPlace,
        fromStateCode: this.fromStateCode,
        reason: this.changeReason,
      }),
    );
  }

  protected async cancel(): Promise<void> {
    await this.run(() =>
      this.service.cancel(this.document(), this.documentId(), {
        reason: this.cancelReason as EwayBillCancelReason,
        remark: this.cancelRemark,
      }),
    );
  }

  private async run(action: () => Promise<EwayBillView>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.bill.set(await action());
      this.mode.set('view');
    } catch (error) {
      const failure = readApiFailure(error);
      this.error.set(failure.detail ? `${failure.text} ${failure.detail}` : failure.text);
    } finally {
      this.busy.set(false);
    }
  }
}
