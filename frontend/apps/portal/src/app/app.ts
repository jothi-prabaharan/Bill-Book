import { ChangeDetectionStrategy } from '@angular/core';
import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule],
  selector: 'bb-portal-root',
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected title = 'portal';
}

