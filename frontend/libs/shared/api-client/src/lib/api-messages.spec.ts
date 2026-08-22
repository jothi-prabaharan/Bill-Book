import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { readApiFailure } from './api-messages';

describe('readApiFailure', () => {
  const failure = (status: number, body: unknown) =>
    readApiFailure(new HttpErrorResponse({ status, error: body }));

  it('API-MSG-01: keeps a refusal’s own words rather than paraphrasing them', () => {
    const result = failure(409, {
      message: 'There is not enough stock available to reserve: C7 - Name 7.',
    });

    // The item names are the only part the person on the phone to the customer
    // can act on, so a generic "conflict" here would be a regression.
    expect(result.text).toBe('There is not enough stock available to reserve: C7 - Name 7.');
    expect(result.status).toBe(409);
  });

  it('API-MSG-02: flattens model validation into one summary and a line per field', () => {
    const result = failure(400, {
      title: 'One or more validation errors occurred.',
      errors: {
        ContactId: ['Choose the customer.'],
        'Lines[0].Quantity': ['Quantity must be greater than zero.'],
      },
    });

    expect(result.text).toBe('Some of what was entered cannot be saved.');
    expect(result.detail).toEqual([
      'Choose the customer.',
      'Quantity must be greater than zero.',
    ]);
  });

  it('API-MSG-03: falls back to the status when the body says nothing', () => {
    expect(failure(403, null).text).toBe('You do not have permission to do this.');
    expect(failure(0, null).text).toBe(
      'The server could not be reached. Check your connection and try again.',
    );
  });

  it('API-MSG-04: a thrown bug in the page is not shown as a server message', () => {
    const result = readApiFailure(new TypeError('cannot read properties of undefined'));

    expect(result.status).toBe(0);
    expect(result.text).toBe('Something went wrong. Try again.');
    expect(result.detail).toEqual([]);
  });
});
