import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface Category {
  itemCategoryId: number;
  categoryCode: string;
  categoryName: string;
  parentCategoryId: number | null;
  defaultItemProfile: string | null;
  defaultCostingType: string | null;
  defaultUomTypeId: number | null;
  displayOrder: number;
  isActive: boolean;
  depth: number;
  itemCount: number;
}

interface UomType {
  uomTypeId: number;
  uomTypeName: string;
}

/**
 * Inventory › Categories. Three levels deep at most — deeper and every report
 * grouping has to decide which level it means.
 *
 * The three defaults are copied onto an item when it is created and then
 * independent. Changing a category's default never rewrites existing items:
 * that would restate costing on stock already held.
 */
@Component({
  selector: 'bb-item-categories-page',
  standalone: true,
  imports: [FormsModule, CdkDropList, CdkDrag, CdkDragHandle],
  templateUrl: './item-categories.page.html',
  styleUrl: './item-categories.page.scss',
})
export class ItemCategoriesPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<Category[]>([]);
  protected readonly types = signal<UomType[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<number | null>(null);

  showInactive = false;
  form = this.blank();

  ngOnInit(): void {
    void this.load();
    void this.loadTypes();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(
        await this.get<Category[]>(`/api/item-categories?includeInactive=${this.showInactive}`),
      );
    } catch {
      this.fail('Could not load categories.');
    } finally {
      this.busy.set(false);
    }
  }

  async loadTypes(): Promise<void> {
    try {
      this.types.set(await this.get<UomType[]>('/api/uom-types'));
    } catch {
      // The unit-type default is optional; the page still works without it.
    }
  }

  startAdd(parentId: number | null): void {
    this.editingId.set(0);
    this.form = { ...this.blank(), parentCategoryId: parentId };
  }

  startEdit(row: Category): void {
    this.editingId.set(row.itemCategoryId);
    this.form = {
      categoryCode: row.categoryCode,
      categoryName: row.categoryName,
      parentCategoryId: row.parentCategoryId,
      defaultItemProfile: row.defaultItemProfile,
      defaultCostingType: row.defaultCostingType,
      defaultUomTypeId: row.defaultUomTypeId,
      isActive: row.isActive,
    };
  }

  async save(): Promise<void> {
    const id = this.editingId();
    if (!this.hasText(this.form.categoryCode) || !this.hasText(this.form.categoryName)) {
      this.fail('Category code and name are required.');
      return;
    }

    const body = {
      ...this.form,
      categoryCode: this.form.categoryCode.trim(),
      categoryName: this.form.categoryName.trim(),
    };

    await this.run(async () => {
      if (id === 0) {
        await this.send('POST', '/api/item-categories', body);
      } else {
        await this.send('PUT', `/api/item-categories/${id}`, body);
      }
      this.editingId.set(null);
    }, 'Category saved.');
  }

  async remove(row: Category): Promise<void> {
    await this.run(
      () => this.send('DELETE', `/api/item-categories/${row.itemCategoryId}`, {}),
      'Category deleted.',
    );
  }

  async onDrop(event: CdkDragDrop<Category[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(
      () =>
        this.send('PATCH', '/api/item-categories/reorder', {
          movedId: moved.itemCategoryId,
          previousId: list[event.currentIndex - 1]?.itemCategoryId ?? null,
          nextId: list[event.currentIndex + 1]?.itemCategoryId ?? null,
        }),
      null,
    );
  }

  /** Only the first two levels may be a parent, since three is the cap. */
  protected get parentOptions(): Category[] {
    return this.rows().filter(
      (c) => c.depth < 3 && c.itemCategoryId !== this.editingId(),
    );
  }

  private blank() {
    return {
      categoryCode: '',
      categoryName: '',
      parentCategoryId: null as number | null,
      defaultItemProfile: null as string | null,
      defaultCostingType: null as string | null,
      defaultUomTypeId: null as number | null,
      isActive: true,
    };
  }

  private async run(action: () => Promise<unknown>, ok: string | null): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      if (ok) {
        this.message.set(ok);
        this.messageIsError.set(false);
      }
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}
