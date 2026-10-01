import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RestaurantSettingsService, RestaurantSettingsView, RequirementLevel } from '@bill-book/restaurant-core';
import { CheckboxComponent, SelectComponent, BbSelectOption, TextInputComponent, PercentageInputComponent } from '@bill-book/ui-components';

@Component({
  selector: 'bb-restaurant-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, CheckboxComponent, SelectComponent, PercentageInputComponent, TextInputComponent],
  template: `
    <div class="p-4 max-w-lg">
      <h2 class="text-xl font-bold mb-4">Restaurant Settings</h2>
      
      <div *ngIf="settings() as s">
        <div class="mb-6 p-4 border rounded bg-gray-50 flex gap-2 flex-wrap">
          <h3 class="font-semibold mb-2 w-full">Mode Presets</h3>
          <button class="px-3 py-1 bg-blue-500 text-white rounded" (click)="applyPreset(false, false)">Billing Only</button>
          <button class="px-3 py-1 bg-blue-500 text-white rounded" (click)="applyPreset(false, true)">Counter + Kitchen</button>
          <button class="px-3 py-1 bg-blue-500 text-white rounded" (click)="applyPreset(true, false)">Dine-in (No Kitchen)</button>
          <button class="px-3 py-1 bg-blue-500 text-white rounded" (click)="applyPreset(true, true)">Full Service</button>
        </div>

        <div class="grid gap-4 mb-4">
          <bb-checkbox label="Tables Enabled" [(ngModel)]="s.tablesEnabled" name="tablesEnabled"></bb-checkbox>
          <bb-checkbox label="Kitchen (KOT) Enabled" [(ngModel)]="s.kitchenEnabled" name="kitchenEnabled"></bb-checkbox>
          <bb-checkbox label="Recipes Enabled" [(ngModel)]="s.recipesEnabled" name="recipesEnabled"></bb-checkbox>

          <bb-select label="Table Required" [(ngModel)]="s.tableRequired" [options]="requirementOptions" name="tableRequired"></bb-select>
          
          <bb-checkbox label="KOT Required" [(ngModel)]="s.kotRequired" name="kotRequired"></bb-checkbox>

          <bb-select label="Customer Required" [(ngModel)]="s.customerRequired" [options]="requirementOptions" name="customerRequired"></bb-select>

          <bb-checkbox label="Waiter Required" [(ngModel)]="s.waiterRequired" name="waiterRequired"></bb-checkbox>

          <bb-percentage-input label="Service Charge (%)" [(ngModel)]="s.serviceChargePercent" name="serviceChargePercent"></bb-percentage-input>

          <bb-checkbox label="Tips Enabled" [(ngModel)]="s.tipsEnabled" name="tipsEnabled"></bb-checkbox>
        </div>

        <button class="px-4 py-2 bg-green-600 text-white font-medium rounded hover:bg-green-700" (click)="save()" [disabled]="saving()">Save Settings</button>
      </div>
    </div>
  `
})
export class RestaurantSettingsPage implements OnInit {
  private readonly service = inject(RestaurantSettingsService);

  readonly requirementOptions: BbSelectOption<RequirementLevel>[] = [
    { value: RequirementLevel.Off, label: 'Off' },
    { value: RequirementLevel.DineInOnly, label: 'Dine-in Only' },
    { value: RequirementLevel.Always, label: 'Always' }
  ];

  settings = signal<RestaurantSettingsView | null>(null);
  saving = signal(false);

  ngOnInit() {
    this.service.getSettings().subscribe(s => this.settings.set(s));
  }

  applyPreset(tables: boolean, kitchen: boolean) {
    const s = this.settings();
    if (s) {
      s.tablesEnabled = tables;
      s.kitchenEnabled = kitchen;
      this.settings.set({ ...s });
    }
  }

  save() {
    const s = this.settings();
    if (s) {
      this.saving.set(true);
      this.service.updateSettings(s).subscribe({
        next: (updated) => {
          this.settings.set(updated);
          this.saving.set(false);
          alert('Settings saved');
        },
        error: () => this.saving.set(false)
      });
    }
  }
}
