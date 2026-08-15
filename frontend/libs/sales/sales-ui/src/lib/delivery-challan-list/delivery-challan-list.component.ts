import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DeliveryChallanService, DeliveryChallanListItem } from '@bill-book/sales-core';
import { RouterModule } from '@angular/router';

/**
 * Sales › Delivery challans. The list.
 *
 * The challan is the document the goods actually leave on, so what this screen
 * has to show that an invoice list does not is **whether they have gone yet**:
 * a draft has moved no stock, a posted one has taken it off the shelf and cannot
 * be taken back by voiding.
 */
@Component({
  selector: 'bb-delivery-challan-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './delivery-challan-list.component.html',
  styleUrl: './delivery-challan-list.component.scss'
})
export class DeliveryChallanListComponent implements OnInit {
  private deliveryChallanService = inject(DeliveryChallanService);

  challans: DeliveryChallanListItem[] = [];
  loading = false;
  error: string | null = null;

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading = true;
    this.error = null;
    this.deliveryChallanService.list().subscribe({
      next: list => {
        this.challans = list;
        this.loading = false;
      },
      error: () => {
        this.error = 'Could not load delivery challans.';
        this.loading = false;
      }
    });
  }

  /** Dispatch: issues the stock and releases the order's reservation. */
  dispatch(challan: DeliveryChallanListItem) {
    this.deliveryChallanService.post(challan.deliveryChallanId).subscribe({
      next: () => this.load(),
      error: () => this.error = `Could not dispatch ${challan.documentNo}.`
    });
  }

  /**
   * Withdraws a draft. The reason is required by the database, not merely by
   * convention, so an empty one is not sent.
   */
  withdraw(challan: DeliveryChallanListItem) {
    const reason = prompt(`Why is ${challan.documentNo} being withdrawn?`);
    if (reason === null || reason.trim() === '') {
      return;
    }

    this.deliveryChallanService
      .voidChallan(challan.deliveryChallanId, { reason: reason.trim() })
      .subscribe({
        next: () => this.load(),
        error: () => this.error = `Could not withdraw ${challan.documentNo}.`
      });
  }

  /** Only a draft can still be dispatched or withdrawn. */
  isDraft(challan: DeliveryChallanListItem): boolean {
    return challan.status === 'Draft' || challan.status === 'ReadyToPost';
  }
}
