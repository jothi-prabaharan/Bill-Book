import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { DOCS } from './docs.manifest';

@Component({
  selector: 'bb-docs-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="layout">
      <aside>
        <a class="brand" routerLink="/index">Bill-Book <span>docs</span></a>
        @for (section of docs; track section.title) {
          <h2>{{ section.title }}</h2>
          <ul>
            @for (page of section.pages; track page.slug) {
              <li>
                <a [routerLink]="'/' + page.slug" routerLinkActive="active">
                  {{ page.title }}
                  @if (page.status !== 'built') {
                    <span class="tag" [class.planned]="page.status === 'planned'">
                      {{ page.status }}
                    </span>
                  }
                </a>
              </li>
            }
          </ul>
        }
      </aside>
      <main><router-outlet /></main>
    </div>
  `,
})
export class AppComponent {
  readonly docs = DOCS;
}
