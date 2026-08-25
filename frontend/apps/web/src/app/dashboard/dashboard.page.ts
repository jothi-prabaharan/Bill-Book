import { ChangeDetectionStrategy } from '@angular/core';
import { Component } from '@angular/core';

/** Placeholder until the real dashboard module lands. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-dashboard-page',
  standalone: true,
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage {}

