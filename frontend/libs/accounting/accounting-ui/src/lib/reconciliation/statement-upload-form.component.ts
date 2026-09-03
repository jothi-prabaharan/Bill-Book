import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

/**
 * Import a bank statement file against an account.
 *
 * Written against plain Angular rather than Ionic, which the first version
 * imported and which is in no package.json in this workspace — the file could
 * not compile, and the whole accounting library went with it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-statement-upload-form',
  standalone: true,
  templateUrl: './statement-upload-form.component.html',
  styleUrl: './statement-upload-form.component.scss',
})
export class StatementUploadFormComponent {
  private readonly http = inject(HttpClient);

  /** The imported statement's id, or null when the form was simply closed. */
  @Output() readonly closed = new EventEmitter<number | null>();

  protected readonly isDragOver = signal(false);
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);

    const dropped = event.dataTransfer?.files;

    if (dropped?.length) {
      this.selectedFile.set(dropped[0]);
    }
  }

  protected onFileSelected(event: Event): void {
    const target = event.target as HTMLInputElement;

    if (target.files?.length) {
      this.selectedFile.set(target.files[0]);
    }
  }

  protected async upload(): Promise<void> {
    const file = this.selectedFile();

    if (!file) {
      return;
    }

    this.uploading.set(true);
    this.error.set(null);

    const form = new FormData();
    form.append('file', file);

    try {
      const result = await this.post<{ bankStatementId: number }>(
        '/api/accounting/bank-statements/upload', form);

      this.closed.emit(result.bankStatementId);
    } catch {
      this.error.set('The statement could not be imported. Check the file and try again.');
    } finally {
      this.uploading.set(false);
    }
  }

  protected dismiss(): void {
    this.closed.emit(null);
  }

  private post<T>(url: string, body: FormData): Promise<T> {
    return new Promise<T>((resolve, reject) => {
      this.http.post<T>(url, body).subscribe({ next: resolve, error: reject });
    });
  }
}
