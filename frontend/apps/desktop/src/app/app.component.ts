import { ChangeDetectionStrategy } from '@angular/core';
import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-desktop-root',
  standalone: true,
  imports: [RouterModule],
  template: '<router-outlet></router-outlet>',
})
export class AppComponent {}

