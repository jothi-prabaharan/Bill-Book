import { describe, expect, it } from 'vitest';
import { isEditable, nextActions, takesParts, WORK_ORDER_STATUSES } from './work-order.models';

describe('work order models (TK-66)', () => {
  it('offer the moves the server allows', () => {
    expect(nextActions('Open')).toEqual(['Assign', 'Cancel']);
    expect(nextActions('InProgress')).toEqual(['Hold', 'Complete']);
    expect(nextActions('Completed')).toEqual([]);
    expect(nextActions('Closed')).toEqual([]);
  });

  it('edit only an open work order, and take parts only while it is under way', () => {
    expect(WORK_ORDER_STATUSES.filter((s) => isEditable(s.value)).map((s) => s.value)).toEqual(['Open']);
    expect(WORK_ORDER_STATUSES.filter((s) => takesParts(s.value)).map((s) => s.value)).toEqual(['Assigned', 'InProgress', 'OnHold']);
  });
});
