import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionContextService } from '@bill-book/auth';

/**
 * The Payroll home page (TK-47). It says where the app stands until its own
 * screens arrive, and links to the settings every app shares.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-home',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './home.page.html',
  styleUrl: './home.page.scss',
})
export class HomePage {
  protected readonly session = inject(SessionContextService);
}
