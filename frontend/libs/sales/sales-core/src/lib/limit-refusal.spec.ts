import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { canRequestApproval } from './limit-refusal';

describe('canRequestApproval', () => {
  it('is true only when the server offers it', () => {
    expect(canRequestApproval(new HttpErrorResponse({ status: 400, error: { message: 'Past the limit.', canRequestApproval: true } }))).toBe(true);
    expect(canRequestApproval(new HttpErrorResponse({ status: 400, error: { message: 'Bad line.' } }))).toBe(false);
    expect(canRequestApproval(new HttpErrorResponse({ status: 400, error: null }))).toBe(false);
    expect(canRequestApproval(new Error('network'))).toBe(false);
  });
});
