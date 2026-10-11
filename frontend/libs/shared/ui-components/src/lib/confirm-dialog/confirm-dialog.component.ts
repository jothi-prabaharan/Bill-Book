import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDialogService } from './confirm-dialog.service';

@Component({
  selector: 'bb-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  protected readonly service = inject(ConfirmDialogService);
  protected readonly state = this.service.state;

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.service.close(false);
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if ((event.target as HTMLElement).classList.contains('dialog-backdrop')) {
      this.service.close(false);
    }
  }

  onConfirm(): void {
    this.service.close(true);
  }

  onCancel(): void {
    this.service.close(false);
  }
}
