import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface SmtpSettings {
  smtpSettingsId: string | null;
  customerId: string | null;
  host: string;
  port: number;
  useSsl: boolean;
  fromEmail: string;
  fromName: string;
  username: string;
  hasPassword: boolean;
  isActive: boolean;
  isInherited: boolean;
}

/**
 * The outbound mail account used for invitations, OTP codes and password
 * resets. The password is write-only: it is never returned by the API, shown as
 * dots, and only sent when the user actually types a new one.
 */
@Component({
  selector: 'bb-smtp-settings-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <h1>Email settings</h1>
    <p class="hint">
      Used to send invitations, verification codes and password resets.
    </p>

    @if (inherited()) {
      <p class="note">
        This organization currently sends using the platform mailbox. Saving here creates your own.
      </p>
    }

    @if (message()) {
      <p [class]="messageIsError() ? 'error' : 'ok'">{{ message() }}</p>
    }

    <form (ngSubmit)="save()">
      <div class="row">
        <label class="grow">
          SMTP host
          <input name="host" [(ngModel)]="form.host" required placeholder="smtp.gmail.com" />
        </label>
        <label class="narrow">
          Port
          <input name="port" type="number" [(ngModel)]="form.port" required />
        </label>
      </div>

      <label class="inline">
        <input name="useSsl" type="checkbox" [(ngModel)]="form.useSsl" />
        Use SSL/TLS
      </label>

      <div class="row">
        <label class="grow">
          From address
          <input name="fromEmail" type="email" [(ngModel)]="form.fromEmail" required />
        </label>
        <label class="grow">
          From name
          <input name="fromName" [(ngModel)]="form.fromName" required placeholder="Bill-Book" />
        </label>
      </div>

      <label>
        Username
        <input name="username" [(ngModel)]="form.username" required />
      </label>

      <label>
        Password
        <input
          name="password"
          type="password"
          [(ngModel)]="form.password"
          [placeholder]="hasPassword() ? '•••••••• (unchanged)' : 'Required'"
          [required]="!hasPassword()"
          autocomplete="new-password"
        />
        <span class="field-hint">
          @if (hasPassword()) {
            Leave blank to keep the stored password. It is encrypted, never shown again.
          } @else {
            Stored encrypted — the mail server needs the real value, so this one secret is
            reversible rather than hashed.
          }
        </span>
      </label>

      <label class="inline">
        <input name="isActive" type="checkbox" [(ngModel)]="form.isActive" />
        Active
      </label>

      <div class="actions">
        <button type="submit" [disabled]="busy()">Save</button>
        <button type="button" class="secondary" (click)="sendTest()" [disabled]="busy() || inherited()">
          Send test email
        </button>
      </div>
    </form>
  `,
  styles: `
    .hint { color: #6a6f80; font-size: .9rem; }
    .note { background: #eef3ff; border-left: 3px solid #3557d6; padding: .5rem .75rem; font-size: .875rem; }
    .error { color: #c0392b; }
    .ok { color: #187a4b; }
    form { display: grid; gap: .9rem; max-width: 34rem; margin-top: 1rem; }
    label { display: grid; gap: .25rem; font-size: .85rem; }
    label.inline { display: flex; align-items: center; gap: .4rem; }
    .row { display: flex; gap: .75rem; }
    .grow { flex: 1; }
    .narrow { width: 6rem; }
    input:not([type='checkbox']) { padding: .5rem; border: 1px solid #cdd1dc; border-radius: 6px; font: inherit; width: 100%; }
    .field-hint { font-size: .75rem; color: #8a8f9e; }
    .actions { display: flex; gap: .75rem; }
    button { padding: .55rem 1rem; border: 0; border-radius: 6px; background: #3557d6; color: #fff; font: inherit; cursor: pointer; }
    button.secondary { background: #eceef3; color: #33384a; }
    button:disabled { opacity: .6; cursor: default; }

    @media (max-width: 600px) {
      .row { flex-direction: column; }
      .narrow { width: 100%; }
    }
  `,
})
export class SmtpSettingsPage implements OnInit {
  private readonly http = inject(HttpClient);

  /** Null targets the platform default row; a customer id targets that customer's override. */
  private readonly customerId = signal<string | null>(localStorage.getItem('bb.customerId'));

  protected readonly busy = signal(false);
  protected readonly hasPassword = signal(false);
  protected readonly inherited = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  form = {
    host: '',
    port: 587,
    useSsl: true,
    fromEmail: '',
    fromName: '',
    username: '',
    password: '',
    isActive: true,
  };

  ngOnInit(): void {
    void this.load();
  }

  private url(): string {
    const id = this.customerId();
    return id ? `/api/smtp-settings/customers/${id}` : '/api/smtp-settings/default';
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const dto = await this.req<SmtpSettings | null>('GET', this.url());
      if (dto) {
        this.form = {
          host: dto.host,
          port: dto.port,
          useSsl: dto.useSsl,
          fromEmail: dto.fromEmail,
          fromName: dto.fromName,
          username: dto.username,
          password: '',
          isActive: dto.isActive,
        };
        this.hasPassword.set(dto.hasPassword && !dto.isInherited);
        this.inherited.set(dto.isInherited);
      }
    } catch {
      this.show('Could not load email settings.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async save(): Promise<void> {
    this.busy.set(true);
    try {
      // Omit the password entirely when untouched, so the stored one survives.
      const body: Record<string, unknown> = { ...this.form };
      if (!this.form.password) {
        delete body['password'];
      }

      await this.req('PUT', this.url(), body);
      this.form.password = '';
      this.show('Email settings saved.', false);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not save email settings.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async sendTest(): Promise<void> {
    const to = prompt('Send a test email to:', this.form.fromEmail);
    if (!to) {
      return;
    }

    this.busy.set(true);
    try {
      const id = this.customerId();
      const url = id ? `/api/smtp-settings/test?customerId=${id}` : '/api/smtp-settings/test';
      const res = await this.req<{ message: string }>('POST', url, { toEmail: to });
      this.show(res.message, false);
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Test send failed.', true);
    } finally {
      this.busy.set(false);
    }
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
