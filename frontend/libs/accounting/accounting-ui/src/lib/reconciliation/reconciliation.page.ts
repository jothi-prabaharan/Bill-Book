import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { StatementUploadFormComponent } from './statement-upload-form.component';

interface LedgerMatch {
  journalLedgerId: number;
  ledgerDate: string;
  transactionTypeCode: string;
  amount: number;
  description: string;
  score: number;
}

interface StatementLine {
  bankStatementLineId: number;
  transactionDate: string;
  description: string;
  referenceNo: string;
  amount: number;
  suggestedMatches: LedgerMatch[];
  isReconciled?: boolean;
}

/**
 * Banking › Reconciliation — an imported statement beside what was recorded.
 *
 * <b>Nothing here posts.</b> The money already moved and the document that
 * recorded it already posted; reconciling compares two accounts of the same
 * events rather than producing a third.
 *
 * Rewritten off Ionic, which this workspace does not have as a dependency, so
 * the page could not compile — and the modal it opened through
 * `ModalController` is now a plain child component, which is how every other
 * sheet in the product works.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-reconciliation-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe, StatementUploadFormComponent],
  templateUrl: './reconciliation.page.html',
  styleUrl: './reconciliation.page.scss',
})
export class ReconciliationPageComponent implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly lines = signal<StatementLine[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly uploading = signal(false);

  /** The statement being worked through. Set by an import. */
  protected readonly currentStatementId = signal<number | null>(null);

  ngOnInit(): void {
    void this.loadSuggestions();
  }

  protected async loadSuggestions(): Promise<void> {
    const statementId = this.currentStatementId();

    if (statementId === null) {
      this.lines.set([]);
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    try {
      const lines = await this.req<StatementLine[]>(
        `/api/accounting/reconciliation/${statementId}/suggestions`);

      this.lines.set(lines ?? []);
    } catch {
      this.error.set('The statement lines could not be read. Try again.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async reconcile(line: StatementLine, match: LedgerMatch): Promise<void> {
    this.error.set(null);

    try {
      await this.post('/api/accounting/reconciliation/reconcile', {
        bankStatementLineId: line.bankStatementLineId,
        journalLedgerId: match.journalLedgerId,
      });

      this.lines.update((all) =>
        all.map((l) =>
          l.bankStatementLineId === line.bankStatementLineId
            ? { ...l, isReconciled: true }
            : l));
    } catch {
      this.error.set('That line could not be tied up. Try again.');
    }
  }

  protected openUpload(): void {
    this.uploading.set(true);
  }

  protected onUploadClosed(bankStatementId: number | null): void {
    this.uploading.set(false);

    if (bankStatementId !== null) {
      this.currentStatementId.set(bankStatementId);
      void this.loadSuggestions();
    }
  }

  private req<T>(url: string): Promise<T> {
    return new Promise<T>((resolve, reject) => {
      this.http.get<T>(url).subscribe({ next: resolve, error: reject });
    });
  }

  private post(url: string, body: unknown): Promise<unknown> {
    return new Promise((resolve, reject) => {
      this.http.post(url, body).subscribe({ next: resolve, error: reject });
    });
  }
}
