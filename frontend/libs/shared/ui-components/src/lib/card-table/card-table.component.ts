import { ChangeDetectionStrategy } from '@angular/core';
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-card-table',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './card-table.component.html',
})
export class CardTableComponent {}

