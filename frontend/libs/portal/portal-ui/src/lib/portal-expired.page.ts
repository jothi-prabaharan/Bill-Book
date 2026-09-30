import { ChangeDetectionStrategy, Component } from '@angular/core';

/** What a contact sees when their portal link has expired or been withdrawn (TK-94). */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-expired',
  standalone: true,
  template: `
    <main class="expired">
      <h1>This link no longer works</h1>
      <p>It has expired, or the business has withdrawn it. Ask them to send you a new portal link.</p>
    </main>
  `,
  styles: [
    `
      .expired {
        max-width: 32rem;
        margin: var(--space-8, 3rem) auto;
        padding: 0 var(--space-4, 1rem);
      }
    `,
  ],
})
export class PortalExpiredPage {}
