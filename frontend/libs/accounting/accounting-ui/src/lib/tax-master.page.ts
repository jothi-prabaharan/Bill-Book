import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface TaxRate {
  taxMasterId: number;
  taxGroupId: number;
  taxSystemName: string | null;
  taxName: string;
  totalRate: number;
  cgstRate: number;
  sgstRate: number;
  igstRate: number;
  cessRate: number;
  effectiveFrom: string;
  effectiveTo: string | null;
  isSales: boolean;
  isPurchase: boolean;
  isActive: boolean;
  isCurrent: boolean;
  isSystemRate: boolean;
}

type Mode = 'create' | 'revise' | 'rename';

/**
 * GST rates. Rates are effective-dated: changing one supersedes it rather than
 * overwriting, so a document dated before the change still resolves the rate
 * that applied then. Renaming is separate, because it is display-only and
 * therefore allowed on seeded rates.
 */
@Component({
  selector: 'bb-tax-master-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <header class="page-head">
      <h1>Tax master</h1>
      <div class="head-actions">
        <label class="inline">
          <input type="checkbox" [(ngModel)]="showHistory" (ngModelChange)="load()" />
          Show superseded
        </label>
        <button type="button" (click)="startCreate()" [disabled]="busy()">+ New rate</button>
      </div>
    </header>

    @if (message()) {
      <p [class]="messageIsError() ? 'error' : 'ok'">{{ message() }}</p>
    }

    @if (mode()) {
      <form class="editor" (ngSubmit)="save()">
        @if (mode() === 'revise') {
          <p class="note">
            This creates a new version. The current one is closed the day before the date below,
            so documents dated earlier keep the old rate.
          </p>
        }
        @if (mode() === 'rename') {
          <p class="note">
            Display name only. This is a standard rate — its split and identity stay fixed, so
            reports and GST returns are unaffected.
          </p>
        }

        <label>
          Name
          <input name="taxName" [(ngModel)]="form.taxName" required maxlength="50" />
        </label>

        @if (mode() !== 'rename') {
          <div class="row">
            <label>
              Total rate %
              <input
                name="totalRate"
                type="number"
                step="0.01"
                min="0"
                [(ngModel)]="form.totalRate"
                (ngModelChange)="recalc()"
                required
              />
            </label>
            <label>
              Cess %
              <input name="cessRate" type="number" step="0.01" min="0" [(ngModel)]="form.cessRate" />
            </label>
          </div>

          <div class="split">
            <div><span>CGST</span><strong>{{ half() }}</strong></div>
            <div><span>SGST</span><strong>{{ half() }}</strong></div>
            <div><span>IGST</span><strong>{{ form.totalRate || 0 }}</strong></div>
            <p class="hint">
              Derived automatically: intra-state splits in half, inter-state carries the full rate.
            </p>
          </div>

          <label>
            Effective from
            <input name="effectiveFrom" type="date" [(ngModel)]="form.effectiveFrom" required />
          </label>

          <fieldset>
            <legend>Applies to</legend>
            <label class="inline">
              <input type="checkbox" name="isSales" [(ngModel)]="form.isSales" />
              Sales <span class="opt">(output tax)</span>
            </label>
            <label class="inline">
              <input type="checkbox" name="isPurchase" [(ngModel)]="form.isPurchase" />
              Purchases <span class="opt">(input tax)</span>
            </label>
            <p class="hint">
              At least one is required. Each direction gets its own CGST, SGST and IGST
              sub-accounts.
            </p>
          </fieldset>
        }

        <div class="editor-actions">
          <button type="submit" [disabled]="busy() || !valid()">Save</button>
          <button type="button" class="link" (click)="mode.set(null)">Cancel</button>
        </div>
      </form>
    } @else {
      <table class="grid">
        <thead>
          <tr>
            <th>Name</th><th>Total</th><th>CGST</th><th>SGST</th><th>IGST</th>
            <th>Applies to</th><th>Effective</th><th></th>
          </tr>
        </thead>
        <tbody>
          @for (r of rates(); track r.taxMasterId) {
            <tr [class.superseded]="!r.isCurrent" [class.inactive]="!r.isActive">
              <td>
                {{ r.taxName }}
                @if (r.isSystemRate) { <span class="badge sys">Standard</span> }
                @if (!r.isCurrent) { <span class="badge old">Superseded</span> }
              </td>
              <td class="num">{{ r.totalRate }}%</td>
              <td class="num">{{ r.cgstRate }}</td>
              <td class="num">{{ r.sgstRate }}</td>
              <td class="num">{{ r.igstRate }}</td>
              <td>
                @if (r.isSales) { <span class="chip">Sales</span> }
                @if (r.isPurchase) { <span class="chip">Purchase</span> }
              </td>
              <td class="dates">
                {{ r.effectiveFrom }}
                @if (r.effectiveTo) { → {{ r.effectiveTo }} }
              </td>
              <td class="row-actions">
                @if (r.isCurrent && r.isActive) {
                  <button type="button" class="link" (click)="startRename(r)">Rename</button>
                  <button type="button" class="link" (click)="startRevise(r)">Revise</button>
                  <button type="button" class="link danger" (click)="deactivate(r)">Deactivate</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="8">No tax rates.</td></tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; }
    .head-actions { display: flex; align-items: center; gap: 1rem; }
    .inline { display: inline-flex; align-items: center; gap: .35rem; font-size: .85rem; }
    .opt { color: #8a8f9e; font-size: .8em; }
    .error { color: #c0392b; }
    .ok { color: #187a4b; }
    .note { background: #fffaf0; border-left: 3px solid #f0b429; padding: .6rem .8rem; font-size: .85rem; }
    .hint { color: #6a6f80; font-size: .78rem; margin: .2rem 0 0; }
    .grid { width: 100%; border-collapse: collapse; margin-top: 1rem; }
    .grid th, .grid td { text-align: left; padding: .5rem .6rem; border-bottom: 1px solid #e2e4ea; }
    .grid th { font-size: .78rem; color: #6a6f80; }
    .grid tr.superseded { opacity: .55; }
    .grid tr.inactive { opacity: .4; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .dates { font-size: .8rem; color: #6a6f80; white-space: nowrap; }
    .badge { font-size: .62rem; padding: .05rem .3rem; border-radius: 3px; margin-left: .3rem; }
    .badge.sys { background: #eceef3; color: #6a6f80; }
    .badge.old { background: #ffe9c7; color: #8a5a00; }
    .chip { font-size: .62rem; background: #e3e9fd; color: #2743a8; padding: .05rem .3rem; border-radius: 3px; margin-right: .25rem; }
    .row-actions { display: flex; gap: .5rem; justify-content: flex-end; }
    .link { background: none; border: 0; color: #3557d6; cursor: pointer; font: inherit; font-size: .85rem; padding: 0; }
    .link.danger { color: #c0392b; }
    button:not(.link) { padding: .5rem .9rem; border: 0; border-radius: 6px; background: #3557d6; color: #fff; font: inherit; cursor: pointer; }
    button:disabled { opacity: .6; cursor: default; }
    .editor { display: grid; gap: .9rem; max-width: 30rem; margin-top: 1rem; }
    .editor label { display: grid; gap: .25rem; font-size: .85rem; }
    .editor label.inline { display: inline-flex; }
    .editor input:not([type='checkbox']) { padding: .5rem; border: 1px solid #cdd1dc; border-radius: 6px; font: inherit; }
    .row { display: flex; gap: .75rem; }
    .row > label { flex: 1; }
    .split { display: grid; grid-template-columns: repeat(3, 1fr); gap: .5rem; background: #f7f8fa; padding: .75rem; border-radius: 8px; }
    .split div { display: grid; gap: .1rem; }
    .split span { font-size: .7rem; color: #8a8f9e; }
    .split strong { font-variant-numeric: tabular-nums; }
    .split .hint { grid-column: 1 / -1; }
    fieldset { border: 1px solid #e2e4ea; border-radius: 8px; display: grid; gap: .35rem; }
    .editor-actions { display: flex; gap: .75rem; align-items: center; }

    @media (max-width: 700px) {
      .grid thead { display: none; }
      .grid tr { display: grid; gap: .15rem; padding: .6rem 0; }
      .grid td { border: 0; padding: .1rem 0; }
      .num { text-align: left; }
      .row-actions { justify-content: flex-start; }
    }
  `,
})
export class TaxMasterPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rates = signal<TaxRate[]>([]);
  protected readonly mode = signal<Mode | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  showHistory = false;
  private editingId: number | null = null;

  form = {
    taxName: '',
    totalRate: 0,
    cessRate: 0,
    effectiveFrom: this.today(),
    isSales: true,
    isPurchase: true,
    isActive: true,
  };

  ngOnInit(): void {
    void this.load();
  }

  /** CGST and SGST are each half the total — shown live as the user types. */
  protected half(): number {
    return Math.round(((this.form.totalRate || 0) / 2) * 100) / 100;
  }

  protected recalc(): void {
    // The split is derived server-side too; this only mirrors it for the user.
  }

  protected valid(): boolean {
    if (this.mode() === 'rename') {
      return this.form.taxName.trim().length > 0;
    }

    return (
      this.form.taxName.trim().length > 0 &&
      (this.form.isSales || this.form.isPurchase) &&
      this.form.totalRate >= 0
    );
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rates.set(
        await this.req<TaxRate[]>('GET', `/api/tax-masters?includeHistory=${this.showHistory}`),
      );
    } catch {
      this.show('Could not load tax rates.', true);
    } finally {
      this.busy.set(false);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.form = {
      taxName: '',
      totalRate: 0,
      cessRate: 0,
      effectiveFrom: this.today(),
      isSales: true,
      isPurchase: true,
      isActive: true,
    };
    this.mode.set('create');
  }

  startRevise(rate: TaxRate): void {
    this.editingId = rate.taxMasterId;
    this.form = {
      taxName: rate.taxName,
      totalRate: rate.totalRate,
      cessRate: rate.cessRate,
      // Defaults to today; must be after the version being replaced.
      effectiveFrom: this.today(),
      isSales: rate.isSales,
      isPurchase: rate.isPurchase,
      isActive: rate.isActive,
    };
    this.mode.set('revise');
  }

  startRename(rate: TaxRate): void {
    this.editingId = rate.taxMasterId;
    this.form = { ...this.form, taxName: rate.taxName };
    this.mode.set('rename');
  }

  async save(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      if (this.mode() === 'create') {
        await this.req('POST', '/api/tax-masters', this.form);
      } else if (this.mode() === 'revise') {
        await this.req('POST', `/api/tax-masters/${this.editingId}/revise`, this.form);
      } else {
        await this.req('PUT', `/api/tax-masters/${this.editingId}/name`, {
          taxName: this.form.taxName,
        });
      }

      this.mode.set(null);
      this.show('Saved.', false);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not save the rate.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(rate: TaxRate): Promise<void> {
    if (!confirm(`Deactivate "${rate.taxName}"? Its sub-accounts are deactivated too.`)) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/tax-masters/${rate.taxMasterId}`);
      await this.load();
    } catch {
      this.show('Could not deactivate the rate.', true);
    } finally {
      this.busy.set(false);
    }
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
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
