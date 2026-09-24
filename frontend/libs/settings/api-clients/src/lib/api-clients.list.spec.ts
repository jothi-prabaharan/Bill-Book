import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ApiClientsListComponent } from './api-clients.list';

/**
 * Settings › API keys talks to Master's `api/master/api-clients` (TK-29). The
 * component's logic only; this workspace's Vitest does not render templateUrl.
 */
describe('ApiClientsListComponent', () => {
  let http: HttpTestingController;
  let page: ApiClientsListComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    page = TestBed.runInInjectionContext(() => new ApiClientsListComponent());
  });

  afterEach(() => http.verify());

  async function flushLoad(): Promise<void> {
    await Promise.resolve();
    http.expectOne('/api/master/api-clients').flush([]);
    http.expectOne('/api/master/api-clients/roles').flush([{ roleId: 4, displayName: 'Sales', isSystemRole: true }]);
    await new Promise((resolve) => setTimeout(resolve));
  }

  it('refuses to create a key without a role and sends nothing', async () => {
    page.newClientName.set('Shop sync');
    await page.createClient();

    expect(page.error()).toContain('choose the role');
    http.expectNone('/api/master/api-clients');
  });

  it('creates a key with its role and shows the key once', async () => {
    page.newClientName.set(' Shop sync ');
    page.newRoleId.set(4);

    const pending = page.createClient();
    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/master/api-clients');
    expect(request.request.body).toEqual({ name: 'Shop sync', roleId: 4 });
    request.flush({ apiKey: 'bb_abc_def', apiClient: { id: 'x', name: 'Shop sync', roleId: 4, roleName: 'Sales', isActive: true, lastUsedAt: null } });
    await flushLoad();
    await pending;

    expect(page.generatedKey()).toBe('bb_abc_def');
    expect(page.newRoleId()).toBeNull();
  });

  it('offers standard roles labelled as such', async () => {
    page.roles.set([
      { roleId: 4, displayName: 'Sales', isSystemRole: true },
      { roleId: 90, displayName: 'Shop sync role', isSystemRole: false },
    ]);

    expect(page.roleOptions()).toEqual([
      { value: 4, label: 'Sales (standard)' },
      { value: 90, label: 'Shop sync role' },
    ]);
  });
});
