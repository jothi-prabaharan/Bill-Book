import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '@bill-book/auth';

/**
 * The whole shell this app needs: a title bar with a sign-out, and the
 * routed page. Not `@bill-book/app-shell` — that component carries the main
 * product's module menu and org switcher, neither of which applies to a
 * single-purpose, single-service admin tool.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  signOut(): void {
    void this.auth.signOut();
    void this.router.navigateByUrl('/login');
  }
}
