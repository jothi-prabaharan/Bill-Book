import { ChangeDetectionStrategy, Component, output, signal } from '@angular/core';

export interface ExportFormat {
  readonly label: string;
  readonly ext: string;
}

@Component({
  selector: 'bb-export-import',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="actions-group">
      <span class="crumb-export">
        <button class="btn btn-secondary" type="button" (click)="toggleExportMenu()" [attr.aria-expanded]="exportMenuOpen()">
          Export
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" class="crumb-export-chevron" [class.open]="exportMenuOpen()"><path d="m6 9 6 6 6-6"/></svg>
        </button>
        @if (exportMenuOpen()) {
          <div class="popover crumb-export-menu" role="menu" aria-label="Export format">
            @for (format of exportFormats; track format.ext) {
              <button class="menuitem menuitem--split" type="button" role="menuitem" (click)="pickExport(format)">
                <span>{{ format.label }}</span>
                <span class="menuitem-ext">{{ format.ext }}</span>
              </button>
            }
          </div>
        }
      </span>
      <button class="btn btn-secondary" type="button" (click)="onOpenImport()">Import</button>
    </div>
  `,
  styles: [`
    .actions-group { display: flex; gap: 0.5rem; align-items: center; }
    .crumb-export { position: relative; }
    .crumb-export-chevron { margin-left: 0.5rem; transition: transform 0.2s; }
    .crumb-export-chevron.open { transform: rotate(180deg); }
    .crumb-export-menu { position: absolute; top: 100%; right: 0; margin-top: 0.25rem; z-index: var(--z-dropdown, 20); min-width: 14rem; }
    .menuitem-ext { color: var(--color-text-subtle); text-transform: uppercase; font-size: 0.7rem; }
    .menuitem--split { display: flex; justify-content: space-between; align-items: center; }
  `]
})
export class ExportImportComponent {
  readonly exportFormats: readonly ExportFormat[] = [
    { label: 'Portable document', ext: 'pdf' },
    { label: 'Excel workbook', ext: 'xlsx' },
    { label: 'Comma separated', ext: 'csv' },
  ];

  readonly exportMenuOpen = signal(false);

  readonly openExport = output<string>();
  readonly openImport = output<void>();

  toggleExportMenu(): void {
    this.exportMenuOpen.update((v) => !v);
  }

  pickExport(format: ExportFormat): void {
    this.exportMenuOpen.set(false);
    this.openExport.emit(format.ext);
  }

  onOpenImport(): void {
    this.openImport.emit();
  }
}
