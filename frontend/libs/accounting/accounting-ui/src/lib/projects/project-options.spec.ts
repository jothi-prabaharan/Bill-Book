import { describe, expect, it } from 'vitest';
import { ProjectListItem, isPostableProject, projectOptions } from './project-options';

const project = (projectId: number, status: ProjectListItem['status']): ProjectListItem => ({
  projectId,
  projectCode: `PRJ-000${projectId}`,
  projectName: `Job ${projectId}`,
  contactId: null,
  billingMethod: 'TimeAndMaterials',
  status,
  startDate: null,
  endDate: null,
  budgetAmount: null,
  currencyCode: 'INR',
});

describe('project options', () => {
  it('offers only the jobs that take postings', () => {
    expect(isPostableProject('Active')).toBe(true);
    expect(isPostableProject('OnHold')).toBe(true);
    expect(isPostableProject('Completed')).toBe(false);
    expect(projectOptions([project(1, 'Active'), project(2, 'Completed')]).map((o) => o.value)).toEqual([1]);
  });

  it('keeps a closed project a line already names', () => {
    expect(projectOptions([project(1, 'Active'), project(2, 'Completed')], [2]).map((o) => o.value)).toEqual([1, 2]);
  });

  it('labels a project by code and name', () => {
    expect(projectOptions([project(3, 'Active')])[0].label).toBe('PRJ-0003 · Job 3');
  });
});
