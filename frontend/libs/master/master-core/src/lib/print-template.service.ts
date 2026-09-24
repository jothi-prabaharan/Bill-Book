import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * The five bands of a printed page, in the order they print.
 *
 * Mirrors `Shared.Kernel.Printing.PrintContent`. The markup is stored with its
 * merge tags — `{{Document.No}}` — and resolved when a document prints.
 */
export interface PrintContent {
  fixedHeaderHtml: string;
  headerHtml: string;
  detailsHtml: string;
  footerHtml: string;
  fixedFooterHtml: string;
}

/**
 * Paper, margins and band positions.
 *
 * Held as the server sends it and sent back untouched: this screen edits the
 * bands, not the page settings, and restating their shape here would be a
 * second copy to keep in step with `PrintSettings`.
 */
export type PrintSettings = Record<string, unknown>;

export interface DocumentTypeOption {
  code: string;
  name: string;
}

export interface PrintTemplateListItem {
  printTemplateId: number;
  templateName: string;
  documentTypeCode: string;
  isDefault: boolean;
  updatedAt: string | null;
}

export interface PrintTemplateDetail {
  printTemplateId: number;
  documentTypeCode: string;
  templateName: string;
  isDefault: boolean;
  isActive: boolean;
  settings: PrintSettings;
  content: PrintContent;
  /** Echoed back on save. A mismatch is refused as stale and nothing is written. */
  templateVersion: number;
  seedVersion: number;
  canResetToLatest: boolean;
  /** Tags this document type cannot resolve. They print as nothing. */
  unknownTags: string[];
  updatedAt: string | null;
}

export interface PlaceholderDefinition {
  tag: string;
  group: string;
  description: string;
}

export interface PlaceholderGroup {
  group: string;
  /** A list group repeats once per row — item lines, tax rows. */
  isList: boolean;
  placeholders: PlaceholderDefinition[];
}

export interface PrintPreview {
  html: string;
  pageCount: number;
  unknownTags: string[];
}

export interface UpdatePrintTemplateRequest {
  templateName: string;
  settings: PrintSettings;
  content: PrintContent;
  templateVersion: number;
}

/**
 * The print template master, served by Printing through the gateway at
 * `api/print-templates`.
 *
 * Every call goes to the branch the user is signed in to: templates are
 * per branch, and the token names the branch, so no id here carries one.
 */
@Injectable({ providedIn: 'root' })
export class PrintTemplateService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/print-templates';

  documentTypes(): Promise<DocumentTypeOption[]> {
    return firstValueFrom(this.http.get<DocumentTypeOption[]>(`${this.base}/document-types`));
  }

  list(documentTypeCode: string): Promise<PrintTemplateListItem[]> {
    const query = new URLSearchParams({ docType: documentTypeCode });
    return firstValueFrom(this.http.get<PrintTemplateListItem[]>(`${this.base}?${query}`));
  }

  placeholders(documentTypeCode: string): Promise<PlaceholderGroup[]> {
    return firstValueFrom(
      this.http.get<PlaceholderGroup[]>(
        `${this.base}/${encodeURIComponent(documentTypeCode)}/placeholders`,
      ),
    );
  }

  get(id: number): Promise<PrintTemplateDetail> {
    return firstValueFrom(this.http.get<PrintTemplateDetail>(`${this.base}/${id}`));
  }

  /**
   * A new template on the platform's generated layout. The first one for a
   * document type becomes its default, so a branch with none can start here.
   */
  create(documentTypeCode: string, templateName: string): Promise<PrintTemplateDetail> {
    return firstValueFrom(
      this.http.post<PrintTemplateDetail>(this.base, { documentTypeCode, templateName }),
    );
  }

  update(id: number, request: UpdatePrintTemplateRequest): Promise<PrintTemplateDetail> {
    return firstValueFrom(this.http.put<PrintTemplateDetail>(`${this.base}/${id}`, request));
  }

  async setDefault(id: number): Promise<void> {
    await firstValueFrom(this.http.patch<void>(`${this.base}/${id}/default`, {}));
  }

  /** Puts the platform's current layout back. Name and page settings are kept. */
  reset(id: number): Promise<PrintTemplateDetail> {
    return firstValueFrom(this.http.post<PrintTemplateDetail>(`${this.base}/${id}/reset`, {}));
  }

  /** The saved template, rendered against sample data. */
  preview(id: number): Promise<PrintPreview> {
    return firstValueFrom(this.http.post<PrintPreview>(`${this.base}/${id}/preview`, {}));
  }
}

/**
 * The bands as the editor names them — by where they print, since that is what
 * a person deciding where the bank details go is asking.
 */
export const PRINT_BANDS: ReadonlyArray<{
  readonly key: keyof PrintContent;
  readonly label: string;
  readonly hint: string;
}> = [
  { key: 'fixedHeaderHtml', label: 'Letterhead', hint: 'Every page, at the top.' },
  { key: 'headerHtml', label: 'Header', hint: 'Page one: the document number, the party and the dates.' },
  { key: 'detailsHtml', label: 'Item lines', hint: 'A row that names a list field repeats once per line.' },
  { key: 'footerHtml', label: 'Totals', hint: 'The last page only.' },
  { key: 'fixedFooterHtml', label: 'Terms', hint: 'Every page, at the bottom.' },
];

/** The merge tag for a placeholder, as the renderer matches it. */
export function mergeTag(tag: string): string {
  return `{{${tag}}}`;
}

/**
 * Puts a merge tag where the cursor is, replacing any selection, and says where
 * the cursor lands after it.
 *
 * Pure, so the one piece of editor behaviour with arithmetic in it can be
 * tested without a textarea. Positions outside the text are clamped rather than
 * trusted: a band that lost focus reports whatever the browser last had.
 */
export function insertTag(
  text: string,
  selectionStart: number | null,
  selectionEnd: number | null,
  tag: string,
): { text: string; caret: number } {
  const clamp = (value: number | null) =>
    value === null || !Number.isFinite(value) ? text.length : Math.min(Math.max(value, 0), text.length);

  const start = clamp(selectionStart);
  const end = Math.max(start, clamp(selectionEnd));
  const inserted = mergeTag(tag);

  return {
    text: text.slice(0, start) + inserted + text.slice(end),
    caret: start + inserted.length,
  };
}
