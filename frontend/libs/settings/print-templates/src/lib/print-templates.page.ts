import { ChangeDetectionStrategy } from '@angular/core';
import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute, Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { SessionContextService } from '@bill-book/auth';
import {
  DocumentTypeOption,
  PRINT_BANDS,
  PlaceholderGroup,
  PrintContent,
  PrintTemplateDetail,
  PrintTemplateListItem,
  PrintTemplateService,
  insertTag,
} from '@bill-book/master-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

/**
 * The print template editor: how each document type looks on paper, per branch.
 *
 * **Five bands, edited as markup with merge tags.** A tag is `{{Document.No}}`,
 * and the Insert-field panel puts one where the cursor is, so nobody has to
 * remember the catalogue's spelling. The server sanitises every band on save and
 * refuses markup it cannot print, naming the band; nothing is sanitised here,
 * because a second, different sanitiser would disagree with the one that counts.
 *
 * **The preview is the server's render of the saved template**, against sample
 * data, by the same renderer a real document prints through. So Save and
 * Preview are one button: a preview of unsaved edits would need a second
 * rendering path, and two paths are two answers.
 *
 * Editing needs `settings.edit`. Anyone with `settings.view` can open the screen
 * and look — the menu offers it to them, so the route has to let them in — but
 * the bands are read-only and the buttons that write are hidden.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-print-templates-page',
  standalone: true,
  imports: [MessageBoxComponent],
  templateUrl: './print-templates.page.html',
  styleUrl: './print-templates.page.scss',
})
export class PrintTemplatesPage implements OnInit {
  private readonly api = inject(PrintTemplateService);
  private readonly session = inject(SessionContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroyRef = inject(DestroyRef);

  private readonly bandElements = viewChildren<ElementRef<HTMLTextAreaElement>>('bandInput');
  private readonly previewSection = viewChild<ElementRef<HTMLElement>>('previewSection');

  protected readonly bands = PRINT_BANDS;
  protected readonly canEdit = computed(() => this.session.has('settings.edit'));

  protected readonly documentTypes = signal<DocumentTypeOption[]>([]);
  protected readonly documentType = signal<string | null>(null);
  protected readonly templates = signal<PrintTemplateListItem[]>([]);
  protected readonly placeholders = signal<PlaceholderGroup[]>([]);

  protected readonly detail = signal<PrintTemplateDetail | null>(null);
  protected readonly draftName = signal('');
  protected readonly draftContent = signal<PrintContent | null>(null);
  protected readonly activeBand = signal<keyof PrintContent>('headerHtml');

  protected readonly previewHtml = signal<SafeHtml | null>(null);
  protected readonly previewPages = signal(0);
  /** At narrow widths the preview is a full-screen sheet, opened on demand. */
  protected readonly previewOpen = signal(false);

  protected readonly newName = signal('');
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly dirty = computed(() => {
    const detail = this.detail();
    const draft = this.draftContent();
    if (detail === null || draft === null) {
      return false;
    }

    return (
      this.draftName().trim() !== detail.templateName ||
      PRINT_BANDS.some((band) => draft[band.key] !== detail.content[band.key])
    );
  });

  protected readonly documentTypeName = computed(
    () =>
      this.documentTypes().find((type) => type.code === this.documentType())?.name ??
      this.documentType() ??
      '',
  );

  ngOnInit(): void {
    void this.start();
  }

  private async start(): Promise<void> {
    try {
      this.documentTypes.set(await this.api.documentTypes());
    } catch (error) {
      this.fail(error);
      return;
    }

    // The route names the type when the menu opened it — one menu row per
    // document type — and the first type otherwise.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const requested = (params.get('docType') ?? '').toUpperCase();
      const known = this.documentTypes().some((type) => type.code === requested);
      void this.openType(known ? requested : (this.documentTypes()[0]?.code ?? null));
    });
  }

  protected onTypeChange(code: string): void {
    void this.router.navigate(['/settings/print-templates', code]);
  }

  private async openType(code: string | null): Promise<void> {
    this.documentType.set(code);
    this.detail.set(null);
    this.draftContent.set(null);
    this.previewHtml.set(null);
    this.templates.set([]);
    this.placeholders.set([]);
    this.messages.set([]);

    if (code === null) {
      return;
    }

    try {
      const [templates, placeholders] = await Promise.all([
        this.api.list(code),
        this.api.placeholders(code),
      ]);

      // A later type chosen while this one loaded wins.
      if (this.documentType() !== code) {
        return;
      }

      this.templates.set(templates);
      this.placeholders.set(placeholders);

      const first = templates.find((t) => t.isDefault) ?? templates[0];
      if (first) {
        await this.select(first.printTemplateId);
      }
    } catch (error) {
      this.fail(error);
    }
  }

  protected async select(id: number): Promise<void> {
    if (this.dirty() && !confirm('Discard the changes you have not saved?')) {
      return;
    }

    try {
      this.apply(await this.api.get(id));
      await this.refreshPreview();
    } catch (error) {
      this.fail(error);
    }
  }

  private apply(detail: PrintTemplateDetail): void {
    this.detail.set(detail);
    this.draftName.set(detail.templateName);
    this.draftContent.set({ ...detail.content });
    this.messages.set(
      detail.unknownTags.length > 0
        ? [
            {
              tone: 'warning',
              text: 'Some fields in this template do not exist for this document type. They print as nothing.',
              detail: detail.unknownTags,
            },
          ]
        : [],
    );
  }

  // ---- Editing -------------------------------------------------------------

  protected onBandInput(key: keyof PrintContent, value: string): void {
    const draft = this.draftContent();
    if (draft !== null) {
      this.draftContent.set({ ...draft, [key]: value });
    }
  }

  /** Puts a field's tag at the cursor in the band last focused. */
  protected insert(tag: string): void {
    const draft = this.draftContent();
    if (draft === null || !this.canEdit()) {
      return;
    }

    const key = this.activeBand();
    const element = this.bandElements().find(
      (ref) => ref.nativeElement.dataset['band'] === key,
    )?.nativeElement;

    const result = insertTag(
      draft[key],
      element?.selectionStart ?? null,
      element?.selectionEnd ?? null,
      tag,
    );

    this.draftContent.set({ ...draft, [key]: result.text });

    if (element) {
      element.value = result.text;
      element.focus();
      element.setSelectionRange(result.caret, result.caret);
    }
  }

  /**
   * Opens the full-screen preview on a phone. Scrolled to, because the content
   * area keeps the editor's scroll position and the preview's Close button would
   * otherwise start off the top of the screen.
   */
  protected openPreview(): void {
    this.previewOpen.set(true);
    setTimeout(() => this.previewSection()?.nativeElement.scrollIntoView({ block: 'start' }));
  }

  // ---- Writes --------------------------------------------------------------

  protected async saveAndPreview(): Promise<void> {
    const detail = this.detail();
    const draft = this.draftContent();
    if (detail === null || draft === null) {
      return;
    }

    await this.run(async () => {
      const saved = await this.api.update(detail.printTemplateId, {
        templateName: this.draftName().trim(),
        settings: detail.settings,
        content: draft,
        templateVersion: detail.templateVersion,
      });

      this.apply(saved);
      await this.reloadList(saved.printTemplateId);
      await this.refreshPreview();
      this.note('Saved.');
    });
  }

  protected async resetLayout(): Promise<void> {
    const detail = this.detail();
    if (
      detail === null ||
      !confirm(
        'Put the standard layout back? Your changes to the bands are replaced; the name and page settings are kept.',
      )
    ) {
      return;
    }

    await this.run(async () => {
      this.apply(await this.api.reset(detail.printTemplateId));
      await this.refreshPreview();
      this.note('The standard layout is back.');
    });
  }

  protected async makeDefault(): Promise<void> {
    const detail = this.detail();
    if (detail === null) {
      return;
    }

    await this.run(async () => {
      await this.api.setDefault(detail.printTemplateId);
      this.apply(await this.api.get(detail.printTemplateId));
      await this.reloadList(detail.printTemplateId);
      this.note('This template now prints by default.');
    });
  }

  protected async create(): Promise<void> {
    const code = this.documentType();
    const name = this.newName().trim();
    if (code === null || name === '') {
      return;
    }

    await this.run(async () => {
      const created = await this.api.create(code, name);
      this.newName.set('');
      this.apply(created);
      await this.reloadList(created.printTemplateId);
      await this.refreshPreview();
    });
  }

  private async reloadList(selectedId: number): Promise<void> {
    const code = this.documentType();
    if (code !== null) {
      this.templates.set(await this.api.list(code));
    }

    if (this.detail()?.printTemplateId !== selectedId) {
      this.apply(await this.api.get(selectedId));
    }
  }

  private async refreshPreview(): Promise<void> {
    const detail = this.detail();
    if (detail === null) {
      this.previewHtml.set(null);
      return;
    }

    const preview = await this.api.preview(detail.printTemplateId);

    // Trusted, not sanitised again: the bands were sanitised by the server
    // when saved, and the frame is sandboxed with no permissions at all, so
    // nothing in it can run. Angular's sanitiser would strip the page styles
    // the renderer depends on and show a preview that is not what prints.
    this.previewHtml.set(this.sanitizer.bypassSecurityTrustHtml(preview.html));
    this.previewPages.set(preview.pageCount);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private note(text: string): void {
    this.messages.set([{ tone: 'success', text }, ...this.messages().filter((m) => m.tone !== 'success')]);
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }
}
