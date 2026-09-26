import { describe, expect, it } from 'vitest';
import { toProjectOptions } from './project-options.service';

describe('toProjectOptions', () => {
  it('offers only open projects, labelled by code and name', () => {
    expect(
      toProjectOptions([
        { projectId: 1, projectCode: 'PRJ-0001', projectName: 'Kitchen', status: 'Active' },
        { projectId: 2, projectCode: 'PRJ-0002', projectName: 'Office', status: 'OnHold' },
        { projectId: 3, projectCode: 'PRJ-0003', projectName: 'Old', status: 'Completed' },
      ]),
    ).toEqual([
      { value: 1, label: 'PRJ-0001 · Kitchen' },
      { value: 2, label: 'PRJ-0002 · Office' },
    ]);
  });
});
