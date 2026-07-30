import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface ConfigurationRow {
  code: string;
  name: string;
  description: string | null;
  dataType: 'Number' | 'Text' | 'Boolean' | 'Date' | 'Json';
  category: string | null;
  defaultValue: string;
  value: string;
  isOverridden: boolean;
}

/**
 * Configuration values, grouped by category. Keys are seed data — this screen
 * edits values and clears overrides; it cannot add or delete keys, because a
 * key nothing reads is dead data and a deleted key breaks whatever read it.
 */
@Component({
  selector: 'bb-configurations-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <h1>Configuration</h1>
    <p class="hint">
      Values apply to this organization. A changed value shows as overridden; resetting returns
      it to the shipped default.
    </p>

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    @for (group of grouped(); track group.category) {
      <section>
        <h2>{{ group.category }}</h2>
        @for (row of group.rows; track row.code) {
          <div class="setting">
            <div class="meta">
              <strong>{{ row.name }}</strong>
              @if (row.isOverridden) {
                <span class="badge">overridden</span>
              }
              @if (row.description) {
                <span class="desc">{{ row.description }}</span>
              }
              <code>{{ row.code }}</code>
            </div>
            <div class="control">
              @switch (row.dataType) {
                @case ('Boolean') {
                  <select [(ngModel)]="row.value" (ngModelChange)="save(row)">
                    <option value="true">Yes</option>
                    <option value="false">No</option>
                  </select>
                }
                @case ('Number') {
                  <input type="number" [(ngModel)]="row.value" (blur)="save(row)" />
                }
                @case ('Date') {
                  <input type="date" [(ngModel)]="row.value" (blur)="save(row)" />
                }
                @default {
                  <input type="text" [(ngModel)]="row.value" (blur)="save(row)" />
                }
              }
              <button
                type="button"
                class="link"
                [disabled]="!row.isOverridden || busy()"
                title="Reset to the shipped default"
                (click)="reset(row)"
              >
                ↻ {{ row.defaultValue }}
              </button>
            </div>
          </div>
        }
      </section>
    } @empty {
      <p>No configuration keys.</p>
    }
  `,
  styles: `
    .hint { color: #6a6f80; font-size: .9rem; }
    .error { color: #c0392b; }
    section { margin-top: 1.5rem; }
    h2 { font-size: .8rem; text-transform: uppercase; letter-spacing: .05em; color: #8a8f9e; }
    .setting {
      display: flex; justify-content: space-between; align-items: center; gap: 1rem;
      padding: .7rem 0; border-bottom: 1px solid #e2e4ea; flex-wrap: wrap;
    }
    .meta { display: flex; flex-direction: column; gap: .1rem; }
    .meta code { font-size: .7rem; color: #8a8f9e; }
    .desc { font-size: .8rem; color: #6a6f80; }
    .badge { font-size: .65rem; background: #ffe9c7; color: #8a5a00; padding: .1rem .35rem; border-radius: 4px; align-self: flex-start; }
    .control { display: flex; align-items: center; gap: .6rem; }
    .control input, .control select { padding: .4rem .5rem; border: 1px solid #cdd1dc; border-radius: 6px; font: inherit; width: 8rem; }
    .link { background: none; border: 0; color: #3557d6; cursor: pointer; font: inherit; font-size: .8rem; }
    .link:disabled { color: #b3b7c2; cursor: default; }

    @media (max-width: 600px) {
      .setting { flex-direction: column; align-items: stretch; }
      .control input, .control select { width: 100%; }
    }
  `,
})
export class ConfigurationsPage implements OnInit {
  private readonly http = inject(HttpClient);

  /** Replace with the org id from the auth token once org context lands. */
  private readonly orgId = signal<string>(localStorage.getItem('bb.orgId') ?? '');

  protected readonly rows = signal<ConfigurationRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly grouped = computed(() => {
    const map = new Map<string, ConfigurationRow[]>();
    for (const row of this.rows()) {
      const key = row.category ?? 'General';
      map.set(key, [...(map.get(key) ?? []), row]);
    }
    return [...map.entries()].map(([category, rows]) => ({ category, rows }));
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(
        await this.req<ConfigurationRow[]>('GET', `/api/organizations/${this.orgId()}/configurations`),
      );
    } catch {
      this.error.set('Could not load configuration.');
    } finally {
      this.busy.set(false);
    }
  }

  async save(row: ConfigurationRow): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.req(
        'PUT',
        `/api/organizations/${this.orgId()}/configurations/${row.code}`,
        { value: row.value },
      );
      await this.load();
    } catch {
      this.error.set(`Could not save ${row.name}.`);
    } finally {
      this.busy.set(false);
    }
  }

  async reset(row: ConfigurationRow): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.req('DELETE', `/api/organizations/${this.orgId()}/configurations/${row.code}`);
      await this.load();
    } catch {
      this.error.set(`Could not reset ${row.name}.`);
    } finally {
      this.busy.set(false);
    }
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
