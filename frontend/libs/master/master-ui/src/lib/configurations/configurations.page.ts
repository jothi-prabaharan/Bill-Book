import { ChangeDetectionStrategy } from '@angular/core';
import { DateInputComponent , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';
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
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-configurations-page',
  standalone: true,
  imports: [FormsModule, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './configurations.page.html',
  styleUrl: './configurations.page.scss',
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

