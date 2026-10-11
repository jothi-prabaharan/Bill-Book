import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

export type IconButtonType = 'edit' | 'delete' | 'trash' | 'add' | 'close' | 'view' | 'copy';
export type IconButtonTone = 'default' | 'danger' | 'primary';

@Component({
  selector: 'bb-icon-button',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './icon-button.component.html',
  styleUrl: './icon-button.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IconButtonComponent {
  readonly icon = input.required<IconButtonType>();
  readonly title = input<string>('');
  readonly tone = input<IconButtonTone>('default');
  readonly disabled = input<boolean>(false);
  readonly ariaLabel = input<string>('');

  readonly actionClick = output<MouseEvent>();

  onClick(event: MouseEvent): void {
    if (!this.disabled()) {
      this.actionClick.emit(event);
    }
  }
}
