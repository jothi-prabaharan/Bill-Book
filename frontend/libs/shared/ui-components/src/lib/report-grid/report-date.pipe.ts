import { Pipe, PipeTransform, inject } from '@angular/core';
import { FormatSettingsService } from '@bill-book/currency-format';
import { reportDate } from './report-format';

/**
 * A report's date in the branch's date pattern (TK-262).
 *
 * Impure on purpose: the branch's formats arrive after the screen has drawn, and a pure pipe
 * would keep the shipped default for as long as its input did not change. It reads a signal, so
 * the template is refreshed when the formats land.
 */
@Pipe({ name: 'reportDate', standalone: true, pure: false })
export class ReportDatePipe implements PipeTransform {
  private readonly formats = inject(FormatSettingsService);

  transform(value: string | null | undefined): string {
    return reportDate(value, this.formats.settings());
  }
}
