import { ChangeDetectionStrategy } from '@angular/core';
import { Component } from '@angular/core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-auth-shell',
  standalone: true,
  templateUrl: './auth-shell.component.html',
})
export class AuthShellComponent {}

