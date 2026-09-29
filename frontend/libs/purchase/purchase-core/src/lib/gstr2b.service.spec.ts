import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { GSTR2B_GROUPS, Gstr2bService } from './gstr2b.service';

describe('Gstr2bService (TK-116)', () => {
  it('uploads the file as multipart under the name "file"', async () => {
    const post = vi.fn().mockReturnValue(of({}));
    TestBed.configureTestingModule({ providers: [Gstr2bService, { provide: HttpClient, useValue: { post } }] });

    await TestBed.inject(Gstr2bService).import(new Blob(['{}']), '2b-june.json');

    expect(post.mock.calls[0][0]).toBe('/api/purchase/gstr2b');
    const form = post.mock.calls[0][1] as FormData;
    expect((form.get('file') as File).name).toBe('2b-june.json');
  });

  it('shows the credit at risk first, then what the books lack', () => {
    expect(GSTR2B_GROUPS.map((g) => g.state)).toEqual(['MissingIn2b', 'MissingInBooks', 'Mismatched', 'Matched']);
  });
});
