import { AppEnvironment } from './environment.model';

export const environment: AppEnvironment = {
  production: true,
  name: 'UAT',
  /**
   * The Gateway's public address for this environment (10 October 2026): the browser
   * reaches the Gateway on 117.247.103.65, and the Gateway reaches the APIs on
   * 192.168.1.101 (see `Gateway.Api/appsettings.*.json`). `window.__BB_CONFIG__.apiBaseUrl`
   * still wins where it is set, so a split deployment needs no rebuild.
   */
  apiBaseUrl: 'http://117.247.103.65:6500',
};
