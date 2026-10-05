/**
 * Recipes and wastage (TK-149): the shapes Inventory's `api/restaurant/recipes`
 * and `api/restaurant/wastage` return, and the pure helpers their pages use.
 */

export const RESTAURANT_RECIPES_API = '/api/restaurant/recipes';
export const RESTAURANT_WASTAGE_API = '/api/restaurant/wastage';

export interface RecipeLine {
  ingredientItemId: number;
  ingredientName: string;
  quantity: number;
  uomId: number;
  uomCode: string;
  cost: number;
}

export interface Recipe {
  menuItemId: number;
  dishName: string;
  categoryName: string;
  price: number | null;
  hasRecipe: boolean;
  yield: number;
  notes: string | null;
  isActive: boolean;
  portionCost: number;
  foodCostPercent: number | null;
  lines: RecipeLine[];
}

export interface UnitOption {
  uomId: number;
  uomTypeId: number;
  uomCode: string;
  uomName: string;
}

export interface RecipeLineDraft {
  ingredientItemId: number | null;
  ingredientName: string;
  uomTypeId: number | null;
  quantity: number | null;
  uomId: number | null;
}

export interface RecipeDraft {
  yield: number | null;
  notes: string;
  isActive: boolean;
  lines: RecipeLineDraft[];
}

export type WastageReason = 'Spoiled' | 'Expired' | 'Burnt' | 'Dropped' | 'Overproduced' | 'ReturnedByGuest' | 'Other';

export const WASTAGE_REASONS: readonly { value: WastageReason; label: string }[] = [
  { value: 'Spoiled', label: 'Spoiled' },
  { value: 'Expired', label: 'Expired' },
  { value: 'Burnt', label: 'Burnt' },
  { value: 'Dropped', label: 'Dropped' },
  { value: 'Overproduced', label: 'Made too much' },
  { value: 'ReturnedByGuest', label: 'Returned by a guest' },
  { value: 'Other', label: 'Other' },
];

export interface WastageEntry {
  wastageEntryId: number;
  entryDate: string;
  itemId: number;
  itemName: string;
  quantity: number;
  uomCode: string;
  reason: WastageReason;
  note: string | null;
  stockAdjustmentId: number;
  adjustmentNo: string;
  status: string;
  cost: number;
}

export function recipeDraftOf(recipe: Recipe, unitTypeOf: (uomId: number) => number | null): RecipeDraft {
  return {
    yield: recipe.hasRecipe ? recipe.yield : 1,
    notes: recipe.notes ?? '',
    isActive: recipe.hasRecipe ? recipe.isActive : true,
    lines: recipe.lines.map((l) => ({ ingredientItemId: l.ingredientItemId, ingredientName: l.ingredientName, uomTypeId: unitTypeOf(l.uomId), quantity: l.quantity, uomId: l.uomId })),
  };
}

/** The first thing wrong with a recipe, in the server's words, or null. */
export function recipeProblem(draft: RecipeDraft, dishId: number): string | null {
  if (!draft.yield || draft.yield <= 0) return 'A recipe must make more than 0 portions.';
  if (!draft.lines.length) return 'Add at least one ingredient.';
  for (const line of draft.lines) {
    if (!line.ingredientItemId) return 'Choose each ingredient.';
    if (line.ingredientItemId === dishId) return 'A dish cannot be an ingredient of itself.';
    if (!line.quantity || line.quantity <= 0) return `Enter how much ${line.ingredientName} the recipe uses.`;
    if (!line.uomId) return `Choose the unit for ${line.ingredientName}.`;
  }
  const ids = draft.lines.map((l) => l.ingredientItemId);
  if (new Set(ids).size !== ids.length) return 'An ingredient is listed twice. Add the quantities together.';
  return null;
}

export function recipeRequest(draft: RecipeDraft) {
  return {
    yield: draft.yield ?? 1,
    notes: draft.notes.trim() || null,
    isActive: draft.isActive,
    lines: draft.lines.map((l) => ({ ingredientItemId: l.ingredientItemId, quantity: l.quantity ?? 0, uomId: l.uomId })),
  };
}

/** How a food cost reads: within a usual 25–35% is fine, over 40% wants looking at. */
export function foodCostTone(percent: number | null): 'good' | 'watch' | 'high' | 'none' {
  if (percent === null) return 'none';
  if (percent > 40) return 'high';
  if (percent > 35) return 'watch';
  return 'good';
}

export function wastageReasonLabel(reason: WastageReason): string {
  return WASTAGE_REASONS.find((r) => r.value === reason)?.label ?? reason;
}
