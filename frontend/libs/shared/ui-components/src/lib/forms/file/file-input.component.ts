import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  forwardRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { BbControlBase } from '../control-base';
import { BbFileSelection, BbRejectedFile } from '../field.model';
import { FormFieldComponent } from '../form-field/form-field.component';

/** One mebibyte, as the size limits are written. */
const MIB = 1024 * 1024;

/**
 * Choosing a file. **Not uploading one.**
 *
 * The component checks the type, the size and the count, lists what was chosen
 * and lets it be removed. The page owns the upload, because the page is what
 * knows the endpoint, the org context, the progress display and what to do when
 * the server refuses.
 *
 * **Nothing here reads a file's contents.** No `FileReader`, no preview, no
 * logging of anything but a name and a byte count — a component that read an
 * attachment to show a thumbnail would be reading whatever the attachment
 * happens to be.
 *
 * The value published is the accepted `File[]`, which is what a `FormData`
 * takes. Rejected files come out on `selectionChange` with a reason so the page
 * can say what happened rather than silently dropping them — a file that
 * vanishes with no explanation reads as a broken screen.
 */
@Component({
  selector: 'bb-file-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => FileInputComponent),
      multi: true,
    },
  ],
  templateUrl: './file-input.component.html',
  styleUrl: './file-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FileInputComponent extends BbControlBase<readonly File[]> {
  /** As the native `accept` attribute: `.csv,.xlsx` or `image/*`. */
  readonly accept = input<string>('');

  readonly multiple = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** Zero or less means no limit. Checked here and again by the API. */
  readonly maxSizeMb = input<number, number | string>(0, {
    transform: (value) => Number(value) || 0,
  });

  /** Only meaningful with `multiple`. Zero means no limit. */
  readonly maxFiles = input<number, number | string>(0, {
    transform: (value) => Number(value) || 0,
  });

  readonly buttonText = input<string>('Choose file');

  readonly valueChange = output<readonly File[]>();

  /** Everything the last pick produced, accepted and refused alike. */
  readonly selectionChange = output<BbFileSelection>();

  protected readonly files = signal<readonly File[]>([]);

  protected readonly rejected = signal<readonly BbRejectedFile[]>([]);

  private readonly picker = viewChild<ElementRef<HTMLInputElement>>('picker');

  protected readonly summary = computed(() => {
    const chosen = this.files();
    if (chosen.length === 0) {
      return 'No file chosen';
    }
    return chosen.length === 1 ? chosen[0].name : `${chosen.length} files chosen`;
  });

  /**
   * A `FormControl` holding files is unusual but legitimate — a page may want
   * `Validators.required` on an attachment. Anything that is not a file list
   * clears the control rather than throwing.
   */
  writeValue(value: unknown): void {
    if (Array.isArray(value)) {
      this.files.set(value.filter((entry): entry is File => entry instanceof File));
    } else if (value instanceof File) {
      this.files.set([value]);
    } else {
      this.files.set([]);
    }
    this.rejected.set([]);
    this.resetPicker();
  }

  protected emitValue(value: readonly File[]): void {
    this.valueChange.emit(value);
  }

  protected onPicked(event: Event): void {
    const element = event.target as HTMLInputElement;
    const picked = Array.from(element.files ?? []);

    const accepted: File[] = [];
    const refused: BbRejectedFile[] = [];
    const limitBytes = this.maxSizeMb() > 0 ? this.maxSizeMb() * MIB : 0;
    const limitCount = this.maxFiles();

    for (const file of picked) {
      if (!this.typeAllowed(file)) {
        refused.push({ name: file.name, size: file.size, reason: 'type' });
        continue;
      }
      if (limitBytes > 0 && file.size > limitBytes) {
        refused.push({ name: file.name, size: file.size, reason: 'size' });
        continue;
      }
      if (limitCount > 0 && accepted.length >= limitCount) {
        refused.push({ name: file.name, size: file.size, reason: 'count' });
        continue;
      }
      accepted.push(file);
    }

    const kept = this.multiple() ? accepted : accepted.slice(0, 1);

    this.files.set(kept);
    this.rejected.set(refused);
    this.publish(kept);
    this.selectionChange.emit({ files: kept, rejected: refused });
    this.onTouched();
  }

  protected remove(index: number): void {
    if (this.effectiveDisabled() || this.readonly()) {
      return;
    }
    const kept = this.files().filter((_, at) => at !== index);
    this.files.set(kept);
    this.publish(kept);
    this.selectionChange.emit({ files: kept, rejected: this.rejected() });
    this.resetPicker();
  }

  protected clearAll(): void {
    if (this.effectiveDisabled() || this.readonly()) {
      return;
    }
    this.files.set([]);
    this.rejected.set([]);
    this.publish([]);
    this.selectionChange.emit({ files: [], rejected: [] });
    this.resetPicker();
  }

  protected sizeOf(file: File): string {
    if (file.size < 1024) {
      return `${file.size} B`;
    }
    if (file.size < MIB) {
      return `${(file.size / 1024).toFixed(1)} KB`;
    }
    return `${(file.size / MIB).toFixed(1)} MB`;
  }

  protected reasonOf(entry: BbRejectedFile): string {
    switch (entry.reason) {
      case 'type':
        return `${entry.name} is not a kind of file this field takes.`;
      case 'size':
        return `${entry.name} is larger than ${this.maxSizeMb()} MB.`;
      default:
        return `${entry.name} was not added — the limit is ${this.maxFiles()} files.`;
    }
  }

  /**
   * The same rule the browser applies to `accept`, applied again.
   *
   * A browser only *filters the dialogue* by `accept`; a file dragged in or
   * chosen through "All files" reaches the input regardless. So the check is
   * repeated here — and repeated a third time by the API, which is the one that
   * counts.
   */
  private typeAllowed(file: File): boolean {
    const accept = this.accept().trim();
    if (accept === '') {
      return true;
    }

    const name = file.name.toLowerCase();
    const type = file.type.toLowerCase();

    return accept.split(',').some((raw) => {
      const rule = raw.trim().toLowerCase();
      if (rule === '') {
        return false;
      }
      if (rule.startsWith('.')) {
        return name.endsWith(rule);
      }
      if (rule.endsWith('/*')) {
        return type.startsWith(rule.slice(0, -1));
      }
      return type === rule;
    });
  }

  /**
   * Clears the element so choosing the same file twice fires `change` again.
   * Without this, removing a file and re-picking it does nothing at all.
   */
  private resetPicker(): void {
    const element = this.picker()?.nativeElement;
    if (element) {
      element.value = '';
    }
  }

  protected idPrefix(): string {
    return 'bb-file';
  }
}
