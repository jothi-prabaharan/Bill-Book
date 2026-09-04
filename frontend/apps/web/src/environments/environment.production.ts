import { AppEnvironment } from './environment.model';

export const environment: AppEnvironment = {
  production: true,
  name: 'Production',
  /**
   * Same origin, which is correct for the documented deployment: the app is
   * served from behind the Gateway, so `/api/...` reaches it without a rewrite.
   *
   * A split deployment — the app on a CDN, the Gateway on its own host — does
   * not change this file. It sets `window.__BB_CONFIG__.apiBaseUrl` where the
   * app is served, and `resolveApiBaseUrl` prefers it. That is deliberate: a
   * hostname here would need a rebuild to change, and would mean the artifact
   * that passed UAT is not the one that ships.
   */
  apiBaseUrl: '',
};
