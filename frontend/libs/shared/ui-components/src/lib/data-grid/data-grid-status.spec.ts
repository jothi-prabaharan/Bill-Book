import { describe, expect, it } from 'vitest';

import { statusTone } from './data-grid-status';

describe('statusTone — a status word is coloured the same way on every list', () => {
  it('GRID-ST-01: what went well reads as success', () => {
    for (const word of ['Paid', 'Posted', 'Approved', 'Active', 'Completed', 'Received', 'Reconciled']) {
      expect(statusTone(word)).toBe('tag-success');
    }
  });

  it('GRID-ST-02: what needs a look reads as warning', () => {
    for (const word of ['Pending', 'Partial', 'Partly paid', 'In progress', 'Ready to post']) {
      expect(statusTone(word)).toBe('tag-warning');
    }
  });

  it('GRID-ST-03: what went wrong reads as danger', () => {
    for (const word of ['Overdue', 'Rejected', 'Cancelled', 'Void', 'Expired', 'Failed']) {
      expect(statusTone(word)).toBe('tag-danger');
    }
  });

  it('GRID-ST-04: a draft or a closed record is only information', () => {
    for (const word of ['Draft', 'Closed', 'Inactive', 'Superseded']) {
      expect(statusTone(word)).toBe('tag-info');
    }
  });

  it('GRID-ST-05: matching ignores case and surrounding space', () => {
    expect(statusTone('  PAID ')).toBe('tag-success');
    expect(statusTone('overdue')).toBe('tag-danger');
  });

  it('GRID-ST-06: a word nobody classified reads as information, never as an alarm', () => {
    expect(statusTone('Whatever the module invented')).toBe('tag-info');
    expect(statusTone(null)).toBe('tag-info');
    expect(statusTone(undefined)).toBe('tag-info');
  });
});
