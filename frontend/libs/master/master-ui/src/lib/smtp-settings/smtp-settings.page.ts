import { ChangeDetectionStrategy } from '@angular/core';
import { TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';
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
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-smtp-settings-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, NumberInputComponent],
  templateUrl: './smtp-settings.page.html',
  styleUrl: './smtp-settings.page.scss',
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
    if (!this.validateForm()) {
      return;
    }

    const body: Record<string, unknown> = {
      ...this.form,
      host: this.form.host.trim(),
      fromEmail: this.form.fromEmail.trim(),
      fromName: this.form.fromName.trim(),
      username: this.form.username.trim(),
      password: this.form.password,
    };

    this.busy.set(true);
    try {
      // Omit the password entirely when untouched, so the stored one survives.
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

  private validateForm(): boolean {
    if (!this.hasText(this.form.host)) {
      this.show('SMTP host is required.', true);
      return false;
    }

    if (!Number.isFinite(this.form.port) || this.form.port < 1 || this.form.port > 65535) {
      this.show('Port must be between 1 and 65535.', true);
      return false;
    }

    if (!this.hasText(this.form.fromEmail)) {
      this.show('From address is required.', true);
      return false;
    }

    if (!this.hasText(this.form.fromName)) {
      this.show('From name is required.', true);
      return false;
    }

    if (!this.hasText(this.form.username)) {
      this.show('Username is required.', true);
      return false;
    }

    if (!this.hasPassword() && !this.hasText(this.form.password)) {
      this.show('Password is required for first-time setup.', true);
      return false;
    }

    return true;
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}

