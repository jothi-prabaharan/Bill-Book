import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'bb-info-tooltip',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="info-tooltip-wrapper" [title]="text()">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="info-icon">
        <circle cx="12" cy="12" r="10"></circle>
        <path d="M12 16v-4"></path>
        <path d="M12 8h.01"></path>
      </svg>
    </span>
  `,
  styles: [`
    :host {
      display: inline-flex;
      align-items: center;
      margin-left: 6px;
      vertical-align: middle;
      cursor: help;
      color: var(--color-ink-light, #64748b);
    }
    .info-tooltip-wrapper {
      display: flex;
    }
    .info-icon:hover {
      color: var(--color-ink, #0f172a);
    }
  `]
})
export class InfoTooltipComponent {
  readonly text = input.required<string>();
}
