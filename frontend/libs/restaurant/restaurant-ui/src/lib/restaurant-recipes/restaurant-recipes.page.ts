import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IfCanDirective } from '@bill-book/auth';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  RESTAURANT_RECIPES_API,
  Recipe,
  RecipeDraft,
  RecipeLineDraft,
  UnitOption,
  foodCostTone,
  recipeDraftOf,
  recipeProblem,
  recipeRequest,
} from '@bill-book/restaurant-core';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

interface UomType {
  uomTypeId: number;
  units: UnitOption[];
}

interface ItemOption {
  itemId: number;
  itemCode: string;
  itemName: string;
}

interface ItemDetail {
  itemId: number;
  itemName: string;
  uomTypeId: number;
  inventoryUomId: number;
  trackInventory: boolean;
}

/**
 * Restaurant › Recipes (TK-149): what each dish is made of. Every dish on the
 * menu is listed with what a portion costs to make at today's average cost of
 * its ingredients, and that cost as a share of its price. Billing a dish takes
 * its ingredients out of stock.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-recipes',
  standalone: true,
  imports: [
    FormsModule,
    IfCanDirective,
    CheckboxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    LookupDialogComponent,
    MessageBoxComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
  ],
  templateUrl: './restaurant-recipes.page.html',
  styleUrl: './restaurant-recipes.page.scss',
})
export class RestaurantRecipesPage implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly recipes = signal<Recipe[]>([]);
  protected readonly units = signal<UnitOption[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly editing = signal<Recipe | null>(null);
  protected draft: RecipeDraft = { yield: 1, notes: '', isActive: true, lines: [] };

  protected readonly pickerOpen = signal(false);
  protected readonly pickerRows = signal<LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);
  private pickingFor: RecipeLineDraft | null = null;

  protected readonly columns: ColumnDef[] = [
    { field: 'dishName', header: 'Dish' },
    { field: 'categoryName', header: 'Category', width: '9rem' },
    { field: 'portionCost', header: 'Portion cost', isTemplate: true, width: '9rem' },
    { field: 'foodCostPercent', header: 'Food cost', isTemplate: true, width: '8rem' },
  ];

  protected readonly withoutRecipe = computed(() => this.recipes().filter((r) => !r.hasRecipe).length);
  protected readonly foodCostTone = foodCostTone;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const [recipes, types] = await Promise.all([
        firstValueFrom(this.http.get<Recipe[]>(RESTAURANT_RECIPES_API)),
        firstValueFrom(this.http.get<UomType[]>('/api/uom-types')),
      ]);
      this.recipes.set(recipes);
      this.units.set(types.flatMap((t) => t.units.map((u) => ({ ...u, uomTypeId: t.uomTypeId }))));
    } catch (error) {
      this.fail(error, 'The recipes could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected unitOptions(line: RecipeLineDraft): BbSelectOption<number>[] {
    return this.units()
      .filter((u) => line.uomTypeId === null || u.uomTypeId === line.uomTypeId)
      .map((u) => ({ value: u.uomId, label: `${u.uomCode} · ${u.uomName}` }));
  }

  protected open(recipe: Recipe): void {
    this.draft = recipeDraftOf(recipe, (uomId) => this.units().find((u) => u.uomId === uomId)?.uomTypeId ?? null);
    this.editing.set(recipe);
    this.messages.set([]);
  }

  protected addLine(): void {
    const line: RecipeLineDraft = { ingredientItemId: null, ingredientName: '', uomTypeId: null, quantity: null, uomId: null };
    this.draft.lines = [...this.draft.lines, line];
    this.openPicker(line);
  }

  protected removeLine(index: number): void {
    this.draft.lines = this.draft.lines.filter((_, i) => i !== index);
  }

  protected openPicker(line: RecipeLineDraft): void {
    this.pickingFor = line;
    this.pickerOpen.set(true);
    void this.search('');
  }

  protected async search(term: string): Promise<void> {
    this.pickerLoading.set(true);
    try {
      const query = new URLSearchParams({ includeInactive: 'false' });
      if (term.trim()) query.set('search', term.trim());
      const items = await firstValueFrom(this.http.get<ItemOption[]>(`/api/items?${query}`));
      this.pickerRows.set(items.map((i) => ({ id: i.itemId, code: i.itemCode, name: i.itemName })));
    } catch {
      this.pickerRows.set([]);
    } finally {
      this.pickerLoading.set(false);
    }
  }

  protected async pick(row: LookupRow): Promise<void> {
    const line = this.pickingFor;
    this.pickerOpen.set(false);
    if (!line) return;
    try {
      const item = await firstValueFrom(this.http.get<ItemDetail>(`/api/items/${row.id}`));
      if (!item.trackInventory) {
        this.messages.set([{ tone: 'error', text: `${item.itemName} is not a stocked item, so a recipe cannot use it up.` }]);
        this.draft.lines = this.draft.lines.filter((l) => l !== line || l.ingredientItemId !== null);
        return;
      }
      line.ingredientItemId = item.itemId;
      line.ingredientName = item.itemName;
      line.uomTypeId = item.uomTypeId;
      line.uomId = item.inventoryUomId;
      this.draft.lines = [...this.draft.lines];
    } catch (error) {
      this.fail(error, 'The ingredient could not be read.');
    }
  }

  protected async save(): Promise<void> {
    const recipe = this.editing();
    if (!recipe) return;
    const problem = recipeProblem(this.draft, recipe.menuItemId);
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.http.put(`${RESTAURANT_RECIPES_API}/${recipe.menuItemId}`, recipeRequest(this.draft)));
      this.editing.set(null);
      this.recipes.set(await firstValueFrom(this.http.get<Recipe[]>(RESTAURANT_RECIPES_API)));
    }, `Recipe for ${recipe.dishName} saved.`);
  }

  protected async remove(): Promise<void> {
    const recipe = this.editing();
    if (!recipe?.hasRecipe || !confirm(`Delete the recipe for ${recipe.dishName}? Billing it will use no ingredients.`)) return;
    await this.run(async () => {
      await firstValueFrom(this.http.delete(`${RESTAURANT_RECIPES_API}/${recipe.menuItemId}`));
      this.editing.set(null);
      this.recipes.set(await firstValueFrom(this.http.get<Recipe[]>(RESTAURANT_RECIPES_API)));
    }, 'Recipe deleted.');
  }

  protected money(value: number): string {
    return this.formats.formatMoney(value);
  }

  private async run(work: () => Promise<void>, done: string): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      this.messages.set([{ tone: 'success', text: done }]);
    } catch (error) {
      this.fail(error, 'That could not be saved.');
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
