import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import {
  MAX_SERVICE_CHARGE_PERCENT,
  RESTAURANT_PRESETS,
  RequirementLevel,
  RestaurantPreset,
  RestaurantSettingsService,
  RestaurantSettingsView,
  applyPreset,
  defaultRestaurantSettings,
  effectiveSettings,
  matchingPreset,
  settingsProblem,
} from '@bill-book/restaurant-core';
import { BbSelectOption, CheckboxComponent, MessageBoxComponent, NumberInputComponent, PercentageInputComponent, SelectComponent, UiMessage } from '@bill-book/ui-components';

/**
 * Settings › Restaurant (D-39, TK-143). Three switches decide which restaurant
 * screens the branch has — tables, the kitchen and recipes — and the rest are
 * the rules an order is checked against. A requirement that depends on a
 * switch is disabled while the switch is off, because the server ignores it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-settings',
  standalone: true,
  imports: [FormsModule, CheckboxComponent, MessageBoxComponent, NumberInputComponent, PercentageInputComponent, SelectComponent],
  templateUrl: './restaurant-settings.page.html',
  styleUrl: './restaurant-settings.page.scss',
})
export class RestaurantSettingsPage implements OnInit {
  private readonly service = inject(RestaurantSettingsService);

  protected readonly presets = RESTAURANT_PRESETS;
  protected readonly maxCharge = MAX_SERVICE_CHARGE_PERCENT;
  protected readonly requirementOptions: BbSelectOption<RequirementLevel>[] = [
    { value: RequirementLevel.Off, label: 'Never' },
    { value: RequirementLevel.DineInOnly, label: 'For dine-in orders' },
    { value: RequirementLevel.Always, label: 'For every order' },
  ];

  protected readonly settings = signal<RestaurantSettingsView>(defaultRestaurantSettings());
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly preset = computed(() => matchingPreset(this.settings()));

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.settings.set(await this.service.load(true));
    } catch (error) {
      this.fail(error, 'The restaurant settings could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected choose(preset: RestaurantPreset): void {
    this.settings.set(applyPreset(this.settings(), preset));
  }

  protected set<K extends keyof RestaurantSettingsView>(key: K, value: RestaurantSettingsView[K]): void {
    this.settings.set(effectiveSettings({ ...this.settings(), [key]: value }));
  }

  protected async save(): Promise<void> {
    const problem = settingsProblem(this.settings());
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    this.saving.set(true);
    try {
      this.settings.set(await this.service.save(this.settings()));
      this.messages.set([{ tone: 'success', text: 'Restaurant settings saved.' }]);
    } catch (error) {
      this.fail(error, 'The restaurant settings could not be saved.');
    } finally {
      this.saving.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
