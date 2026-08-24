import { ChangeDetectionStrategy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { marked } from 'marked';
import { ALL_PAGES } from '../docs.manifest';

/** Loads a markdown file from content/ and renders it. */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-doc-viewer',
  standalone: true,
  templateUrl: './doc-viewer.component.html',
  styleUrl: './doc-viewer.component.scss',
})
export class DocViewerComponent {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  protected readonly html = signal<string>('');
  protected readonly slug = signal<string>('index');
  protected readonly missing = signal(false);

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug') ?? 'index';
      this.slug.set(slug);
      void this.load(slug);
    });
  }

  private async load(slug: string): Promise<void> {
    this.missing.set(false);

    // Only serve slugs the manifest knows — no arbitrary path fetching.
    if (!ALL_PAGES.some((p) => p.slug === slug)) {
      this.missing.set(true);
      return;
    }

    try {
      const markdown = await new Promise<string>((resolve, reject) =>
        this.http
          .get(`content/${slug}.md`, { responseType: 'text' })
          .subscribe({ next: resolve, error: reject }),
      );
      this.html.set(await marked.parse(markdown));
    } catch {
      this.missing.set(true);
    }
  }
}

