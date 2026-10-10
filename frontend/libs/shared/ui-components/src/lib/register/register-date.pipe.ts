import { Pipe, PipeTransform } from '@angular/core';
import { registerDate } from './register-format';

/** A register row's date: `11 Aug 2026` (TK-187). */
@Pipe({ name: 'registerDate', standalone: true })
export class RegisterDatePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return registerDate(value);
  }
}
