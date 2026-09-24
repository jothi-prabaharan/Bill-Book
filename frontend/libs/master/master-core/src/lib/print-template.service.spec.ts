import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  PRINT_BANDS,
  PrintTemplateService,
  insertTag,
  mergeTag,
} from './print-template.service';

/**
 * Which URL and verb each call uses — the editor's whole contract with Printing —
 * and the one piece of editor arithmetic, putting a tag at the cursor.
 */
describe('PrintTemplateService', () => {
  let service: PrintTemplateService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PrintTemplateService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists one document type’s templates', async () => {
    const pending = service.list('INV');
    httpMock.expectOne('/api/print-templates?docType=INV').flush([]);
    expect(await pending).toEqual([]);
  });

  it('reads the document types and a type’s placeholders', async () => {
    const types = service.documentTypes();
    httpMock.expectOne('/api/print-templates/document-types').flush([{ code: 'INV', name: 'Invoice' }]);
    expect(await types).toEqual([{ code: 'INV', name: 'Invoice' }]);

    const fields = service.placeholders('INV');
    httpMock.expectOne('/api/print-templates/INV/placeholders').flush([]);
    expect(await fields).toEqual([]);
  });

  it('creates from the standard layout by sending only the type and the name', async () => {
    const pending = service.create('INV', 'Branch layout');
    const request = httpMock.expectOne('/api/print-templates');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ documentTypeCode: 'INV', templateName: 'Branch layout' });

    request.flush({ printTemplateId: 7 });
    expect((await pending).printTemplateId).toBe(7);
  });

  it('saves with PUT and echoes the version it read', async () => {
    const pending = service.update(7, {
      templateName: 'A4',
      settings: { paperSize: 0 },
      content: {
        fixedHeaderHtml: '',
        headerHtml: '<div>{{Document.No}}</div>',
        detailsHtml: '',
        footerHtml: '',
        fixedFooterHtml: '',
      },
      templateVersion: 3,
    });

    const request = httpMock.expectOne('/api/print-templates/7');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.templateVersion).toBe(3);
    expect(request.request.body.settings).toEqual({ paperSize: 0 });

    request.flush({ printTemplateId: 7, templateVersion: 4 });
    expect((await pending).templateVersion).toBe(4);
  });

  it('sets the default with PATCH, and resets and previews with POST', async () => {
    const setDefault = service.setDefault(7);
    const patch = httpMock.expectOne('/api/print-templates/7/default');
    expect(patch.request.method).toBe('PATCH');
    patch.flush(null);
    await setDefault;

    const reset = service.reset(7);
    const post = httpMock.expectOne('/api/print-templates/7/reset');
    expect(post.request.method).toBe('POST');
    post.flush({ printTemplateId: 7 });
    await reset;

    const preview = service.preview(7);
    const previewRequest = httpMock.expectOne('/api/print-templates/7/preview');
    expect(previewRequest.request.method).toBe('POST');
    previewRequest.flush({ html: '<div></div>', pageCount: 1, unknownTags: [] });
    expect((await preview).pageCount).toBe(1);
  });
});

describe('insertTag', () => {
  it('puts the tag at the cursor and moves the cursor past it', () => {
    expect(insertTag('No: ', 4, 4, 'Document.No')).toEqual({
      text: 'No: {{Document.No}}',
      caret: 19,
    });
  });

  it('replaces a selection', () => {
    expect(insertTag('Hello NAME!', 6, 10, 'Party.Name')).toEqual({
      text: 'Hello {{Party.Name}}!',
      caret: 20,
    });
  });

  it('appends when the band never had a cursor', () => {
    expect(insertTag('<p>', null, null, 'Organization.Name').text).toBe('<p>{{Organization.Name}}');
  });

  it('clamps positions the browser reports outside the text', () => {
    expect(insertTag('ab', 99, 120, 'X').text).toBe('ab{{X}}');
    expect(insertTag('ab', -5, -1, 'X').text).toBe('{{X}}ab');
  });

  it('treats an end before the start as an empty selection', () => {
    expect(insertTag('abcd', 3, 1, 'X')).toEqual({ text: 'abc{{X}}d', caret: 8 });
  });
});

describe('the bands', () => {
  it('are the five of PrintContent, in the order they print', () => {
    expect(PRINT_BANDS.map((band) => band.key)).toEqual([
      'fixedHeaderHtml',
      'headerHtml',
      'detailsHtml',
      'footerHtml',
      'fixedFooterHtml',
    ]);
  });

  it('write a tag the way the renderer matches it', () => {
    expect(mergeTag('Item.Rate')).toBe('{{Item.Rate}}');
  });
});
