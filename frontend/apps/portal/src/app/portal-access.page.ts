import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PortalSession } from './portal-session';

/**
 * Where a portal link lands (`/portal?token=…`, TK-69): keeps the token and
 * sends a School guardian to the parent portal, anyone else to their statement.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-access',
  standalone: true,
  template: '<p>Opening your portal…</p>',
})
export class PortalAccessPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly session = inject(PortalSession);

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (token) this.session.set(token);
    void this.router.navigateByUrl(this.session.app() === 'School' ? '/school' : '/dashboard', { replaceUrl: true });
  }
}
