import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { PortalApi } from '../retail/portal-api.service';
import { PortalQuoteItem, quoteState } from '../retail/portal.models';

/**
 * The contact's quotes (TK-96): each posted quote with its validity, and
 * accept or decline while it is still open, once, with a name and a note.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-quotes',
  standalone: true,
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink],
  templateUrl: './portal-quotes.page.html',
  styleUrl: '../retail/retail-portal.scss',
})
export class PortalQuotesPage implements OnInit {
  private readonly api = inject(PortalApi);

  protected readonly quotes = signal<PortalQuoteItem[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly answering = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly today = new Date().toISOString().slice(0, 10);
  protected readonly quoteState = quoteState;

  protected name = '';
  protected note = '';

  ngOnInit(): void {
    void this.load();
  }

  protected open(quoteId: number): void {
    this.answering.set(quoteId);
    this.error.set(null);
  }

  protected async answer(quoteId: number, answer: 'accept' | 'reject'): Promise<void> {
    if (!this.name.trim()) {
      this.error.set('Enter your name so the business knows who answered.');
      return;
    }
    this.busy.set(true);
    try {
      await this.api.answerQuote(quoteId, answer, this.name.trim(), this.note.trim() || null);
      this.answering.set(null);
      this.note = '';
      await this.load();
    } catch (err: unknown) {
      const message = err instanceof HttpErrorResponse ? (err.error as { message?: string } | null)?.message : undefined;
      this.error.set(message ?? 'Your answer could not be sent. Try again in a moment.');
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.quotes.set(await this.api.quotes());
    } catch {
      this.error.set('Your quotes could not be loaded. Try again in a moment.');
    } finally {
      this.loading.set(false);
    }
  }
}
