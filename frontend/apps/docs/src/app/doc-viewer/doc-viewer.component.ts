import { ChangeDetectionStrategy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { marked } from 'marked';
import { ALL_PAGES } from '../docs.manifest';

// Configure custom marked renderer to wrap screenshots in a browser frame
const renderer = new marked.Renderer();
// eslint-disable-next-line @typescript-eslint/no-explicit-any
renderer.image = (arg1: any, arg2?: any, arg3?: any) => {
  let href = '';
  let title = '';
  let text = '';
  if (typeof arg1 === 'object' && arg1 !== null) {
    href = arg1.href ?? '';
    title = arg1.title ?? '';
    text = arg1.text ?? '';
  } else {
    href = arg1 ?? '';
    title = arg2 ?? '';
    text = arg3 ?? '';
  }
  const caption = text || title || 'Bill-Book Cloud ERP';
  return `<figure class="browser-frame">
    <div class="browser-frame__bar">
      <div class="browser-frame__dots">
        <span class="dot dot-red"></span>
        <span class="dot dot-yellow"></span>
        <span class="dot dot-green"></span>
      </div>
      <div class="browser-frame__url">
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect><path d="M7 11V7a5 5 0 0 1 10 0v4"></path></svg>
        <span>app.billbook.in</span>
      </div>
    </div>
    <div class="browser-frame__viewport">
      <img src="${href}" alt="${caption}" loading="lazy" />
    </div>
    ${caption ? `<figcaption class="browser-frame__caption">${caption}</figcaption>` : ''}
  </figure>`;
};
marked.use({ renderer });

/** Loads a markdown file from content/ and renders it. */
@Component({
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

