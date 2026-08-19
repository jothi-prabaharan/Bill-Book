import { Directive, Input, TemplateRef, inject } from '@angular/core';

@Directive({
  selector: '[bbCellTemplate]',
  standalone: true
})
export class DataGridCellTemplateDirective {
  @Input('bbCellTemplate') fieldName!: string;

  public template = inject(TemplateRef<any>);
}
