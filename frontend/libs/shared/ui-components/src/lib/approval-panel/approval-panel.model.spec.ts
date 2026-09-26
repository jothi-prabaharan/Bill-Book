import { describe, expect, it } from 'vitest';
import { canSubmitForApproval, stepStatusLabel } from './approval-panel.model';

describe('approval panel (TK-100)', () => {
  it('offers submit only for a draft that is not already waiting', () => {
    expect(canSubmitForApproval('Draft', null)).toBe(true);
    expect(canSubmitForApproval('Draft', { approvalStatus: 'Rejected', steps: [], canAct: false })).toBe(true);
    expect(canSubmitForApproval('Draft', { approvalStatus: 'InApproval', steps: [], canAct: false })).toBe(false);
    expect(canSubmitForApproval('ReadyToPost', { approvalStatus: 'Approved', steps: [], canAct: false })).toBe(false);
  });

  it('reads a step status in plain words', () => {
    expect(stepStatusLabel('Pending')).toBe('Waiting for approval');
    expect(stepStatusLabel('Waiting')).toBe('Not reached yet');
    expect(stepStatusLabel('SentBack')).toBe('Sent back');
    expect(stepStatusLabel('Approved')).toBe('Approved');
  });
});
