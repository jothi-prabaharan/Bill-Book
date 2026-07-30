import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface AccountRow {
  accountId: number;
  accountTypeId: number;
  accountCode: string;
  accountName: string;
  parentAccountId: number | null;
  currencyCode: string | null;
  isContra: boolean;
  isSystemDefault: boolean;
  isActive: boolean;
  isUsed: boolean;
  isLock: boolean;
  isJE: boolean;
  isSales: boolean;
  isPurchase: boolean;
  isPayment: boolean;
  isBank: boolean;
  isConfigLocked: boolean;
}

interface AccountType {
  accountTypeId: number;
  displayName: string;
  normalBalance: string;
  reportSection: string;
  sortOrder: number;
}

/**
 * Chart of accounts, grouped by account type. Once an account has been used its
 * type, code and usage flags are frozen — only the display name, active state
 * and posting lock stay editable. IsJE is never shown: it is backend-only.
 */
@Component({
  selector: 'bb-chart-of-accounts-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <header class="page-head">
      <h1>Chart of accounts</h1>
      <div class="head-actions">
        <label class="inline">
          <input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />
          Show inactive
        </label>
        <button type="button" (click)="startCreate()" [disabled]="busy()">+ New account</button>
      </div>
    </header>

    @if (message()) {
      <p [class]="messageIsError() ? 'error' : 'ok'">{{ message() }}</p>
    }

    @if (editing()) {
      <form class="editor" (ngSubmit)="save()">
        @if (locked()) {
          <p class="note">
            This account has been used, so its type, code and usage flags are fixed. Changing them
            would silently reclassify postings that already exist. Name, active state and posting
            lock can still change.
          </p>
        }

        <div class="row">
          <label>
            Type
            <select name="accountTypeId" [(ngModel)]="form.accountTypeId" [disabled]="locked()" required>
              @for (t of types(); track t.accountTypeId) {
                <option [ngValue]="t.accountTypeId">{{ t.displayName }}</option>
              }
            </select>
          </label>
          <label>
            Code
            <input name="accountCode" [(ngModel)]="form.accountCode" [disabled]="locked()" required />
          </label>
        </div>

        <label>
          Name
          <input name="accountName" [(ngModel)]="form.accountName" required />
        </label>

        <label>
          Parent account <span class="opt">(same type only)</span>
          <select name="parentAccountId" [(ngModel)]="form.parentAccountId">
            <option [ngValue]="null">— none —</option>
            @for (a of parentOptions(); track a.accountId) {
              <option [ngValue]="a.accountId">{{ a.accountCode }} · {{ a.accountName }}</option>
            }
          </select>
        </label>

        <fieldset>
          <legend>Usage</legend>
          <div class="flags">
            <label class="inline">
              <input type="checkbox" [(ngModel)]="form.isSales" name="isSales" [disabled]="locked()" />
              Sales documents
            </label>
            <label class="inline">
              <input type="checkbox" [(ngModel)]="form.isPurchase" name="isPurchase" [disabled]="locked()" />
              Purchase documents
            </label>
            <label class="inline">
              <input type="checkbox" [(ngModel)]="form.isPayment" name="isPayment" [disabled]="locked()" />
              Payments
            </label>
            <label class="inline">
              <input type="checkbox" [(ngModel)]="form.isBank" name="isBank" [disabled]="locked()" />
              Bank / cash account
            </label>
            <label class="inline">
              <input type="checkbox" [(ngModel)]="form.isContra" name="isContra" [disabled]="locked()" />
              Contra <span class="opt">(reports subtract)</span>
            </label>
          </div>
        </fieldset>

        <div class="row">
          <label class="inline">
            <input type="checkbox" [(ngModel)]="form.isActive" name="isActive" />
            Active
          </label>
          <label class="inline">
            <input type="checkbox" [(ngModel)]="form.isLock" name="isLock" />
            Posting locked <span class="opt">(freeze all posting)</span>
          </label>
        </div>

        <div class="editor-actions">
          <button type="submit" [disabled]="busy()">Save</button>
          <button type="button" class="link" (click)="editing.set(false)">Cancel</button>
        </div>
      </form>
    } @else {
      @for (group of grouped(); track group.type.accountTypeId) {
        <section>
          <h2>
            {{ group.type.displayName }}
            <span class="meta">{{ group.type.normalBalance }} · {{ group.type.reportSection }}</span>
          </h2>
          <table class="grid">
            <tbody>
              @for (a of group.accounts; track a.accountId) {
                <tr [class.inactive]="!a.isActive" [class.child]="a.parentAccountId">
                  <td class="code">{{ a.accountCode }}</td>
                  <td>
                    {{ a.accountName }}
                    @if (a.isSystemDefault) { <span class="badge sys">System</span> }
                    @if (a.isContra) { <span class="badge contra">Contra</span> }
                    @if (a.isLock) { <span class="badge lock">Locked</span> }
                  </td>
                  <td class="flags-cell">
                    @if (a.isBank) { <span class="chip">Bank</span> }
                    @if (a.isSales) { <span class="chip">Sales</span> }
                    @if (a.isPurchase) { <span class="chip">Purchase</span> }
                    @if (a.isPayment) { <span class="chip">Payment</span> }
                    @if (a.isJE) { <span class="chip je">Journal</span> }
                  </td>
                  <td class="row-actions">
                    <button type="button" class="link" (click)="startEdit(a)">Edit</button>
                    @if (!a.isSystemDefault && a.isActive) {
                      <button type="button" class="link danger" (click)="deactivate(a)">Deactivate</button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="4" class="empty">No accounts of this type.</td></tr>
              }
            </tbody>
          </table>
        </section>
      }
    }
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
    .head-actions { display: flex; align-items: center; gap: 1rem; }
    .inline { display: inline-flex; align-items: center; gap: .35rem; font-size: .85rem; }
    .opt { color: #8a8f9e; font-weight: 400; font-size: .8em; }
    .error { color: #c0392b; }
    .ok { color: #187a4b; }
    .note { background: #fffaf0; border-left: 3px solid #f0b429; padding: .6rem .8rem; font-size: .85rem; }
    section { margin-top: 1.75rem; }
    h2 { font-size: 1rem; border-bottom: 1px solid #e2e4ea; padding-bottom: .3rem; display: flex; gap: .75rem; align-items: baseline; }
    .meta { font-size: .72rem; color: #8a8f9e; font-weight: 400; }
    .grid { width: 100%; border-collapse: collapse; }
    .grid td { padding: .45rem .6rem; border-bottom: 1px solid #f0f1f5; vertical-align: top; }
    .grid tr.inactive { opacity: .5; }
    .grid tr.child .code { padding-left: 1.75rem; }
    .code { font-variant-numeric: tabular-nums; color: #6a6f80; width: 6rem; }
    .empty { color: #8a8f9e; font-size: .85rem; }
    .badge { font-size: .62rem; padding: .05rem .3rem; border-radius: 3px; margin-left: .3rem; }
    .badge.sys { background: #eceef3; color: #6a6f80; }
    .badge.contra { background: #ffe9c7; color: #8a5a00; }
    .badge.lock { background: #ffd9d6; color: #a3271c; }
    .chip { font-size: .62rem; background: #e3e9fd; color: #2743a8; padding: .05rem .3rem; border-radius: 3px; margin-right: .25rem; }
    .chip.je { background: #d8f5e3; color: #187a4b; }
    .flags-cell { white-space: nowrap; }
    .row-actions { display: flex; gap: .5rem; justify-content: flex-end; }
    .link { background: none; border: 0; color: #3557d6; cursor: pointer; font: inherit; font-size: .85rem; padding: 0; }
    .link.danger { color: #c0392b; }
    button:not(.link) { padding: .5rem .9rem; border: 0; border-radius: 6px; background: #3557d6; color: #fff; font: inherit; cursor: pointer; }
    .editor { display: grid; gap: .9rem; max-width: 34rem; margin-top: 1rem; }
    .editor label { display: grid; gap: .25rem; font-size: .85rem; }
    .editor label.inline { display: inline-flex; }
    .editor input:not([type='checkbox']), .editor select { padding: .5rem; border: 1px solid #cdd1dc; border-radius: 6px; font: inherit; }
    .row { display: flex; gap: .75rem; }
    .row > label { flex: 1; }
    fieldset { border: 1px solid #e2e4ea; border-radius: 8px; }
    .flags { display: grid; gap: .4rem; }
    .editor-actions { display: flex; gap: .75rem; align-items: center; }

    @media (max-width: 640px) {
      .row { flex-direction: column; }
      .grid td { display: block; border: 0; padding: .1rem .6rem; }
      .grid tr { display: block; padding: .5rem 0; border-bottom: 1px solid #f0f1f5; }
      .row-actions { justify-content: flex-start; }
    }
  `,
})
export class ChartOfAccountsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly accounts = signal<AccountRow[]>([]);
  protected readonly types = signal<AccountType[]>([]);
  protected readonly editing = signal(false);
  protected readonly locked = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  showInactive = false;
  private editingId: number | null = null;

  form = {
    accountTypeId: 1,
    accountCode: '',
    accountName: '',
    parentAccountId: null as number | null,
    currencyCode: null as string | null,
    isContra: false,
    isActive: true,
    isLock: false,
    isSales: false,
    isPurchase: false,
    isPayment: false,
    isBank: false,
  };

  /** Accounts grouped under their type, parents before their children. */
  protected readonly grouped = computed(() =>
    this.types()
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((type) => ({
        type,
        accounts: this.accounts()
          .filter((a) => a.accountTypeId === type.accountTypeId)
          .sort(
            (a, b) =>
              (a.parentAccountId ?? a.accountId) - (b.parentAccountId ?? b.accountId) ||
              a.accountCode.localeCompare(b.accountCode),
          ),
      })),
  );

  /** Only same-type accounts may parent, so the tree cannot cross report sections. */
  protected readonly parentOptions = computed(() =>
    this.accounts().filter(
      (a) => a.accountTypeId === this.form.accountTypeId && a.accountId !== this.editingId,
    ),
  );

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.types.set(await this.req<AccountType[]>('GET', '/api/master/account-types'));
      this.accounts.set(
        await this.req<AccountRow[]>('GET', `/api/accounts?includeInactive=${this.showInactive}`),
      );
    } catch {
      this.show('Could not load the chart of accounts.', true);
    } finally {
      this.busy.set(false);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.locked.set(false);
    this.form = {
      accountTypeId: this.types()[0]?.accountTypeId ?? 1,
      accountCode: '',
      accountName: '',
      parentAccountId: null,
      currencyCode: null,
      isContra: false,
      isActive: true,
      isLock: false,
      isSales: false,
      isPurchase: false,
      isPayment: false,
      isBank: false,
    };
    this.editing.set(true);
  }

  startEdit(account: AccountRow): void {
    this.editingId = account.accountId;
    this.locked.set(account.isConfigLocked);
    this.form = {
      accountTypeId: account.accountTypeId,
      accountCode: account.accountCode,
      accountName: account.accountName,
      parentAccountId: account.parentAccountId,
      currencyCode: account.currencyCode,
      isContra: account.isContra,
      isActive: account.isActive,
      isLock: account.isLock,
      isSales: account.isSales,
      isPurchase: account.isPurchase,
      isPayment: account.isPayment,
      isBank: account.isBank,
    };
    this.editing.set(true);
  }

  async save(): Promise<void> {
    this.busy.set(true);
    try {
      if (this.editingId === null) {
        await this.req('POST', '/api/accounts', this.form);
      } else {
        await this.req('PUT', `/api/accounts/${this.editingId}`, this.form);
      }
      this.editing.set(false);
      this.show('Account saved.', false);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not save the account.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(account: AccountRow): Promise<void> {
    if (!confirm(`Deactivate "${account.accountName}"?`)) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/accounts/${account.accountId}`);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not deactivate the account.', true);
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
