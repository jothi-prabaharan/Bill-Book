import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PortalSession } from './portal-session';

/**
 * Where a portal link lands (`/access/:code`, TK-94): exchanges the code for a
 * session and sends a School guardian to the parent portal, anyone else to
 * their dashboard. A link that no longer works goes to the expired page. So
 * does an old `/portal?token=…` link: those stopped working when links became
 * revocable.
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
    void this.open();
  }

  private async open(): Promise<void> {
    const code = this.route.snapshot.paramMap.get('code');
    const opened = code ? await this.session.start(code) : false;

    const target = !opened ? '/expired' : this.session.app() === 'School' ? '/school' : '/dashboard';
    await this.router.navigateByUrl(target, { replaceUrl: true });
  }
}
