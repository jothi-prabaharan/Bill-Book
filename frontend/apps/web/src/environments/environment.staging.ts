import { AppEnvironment } from './environment.model';

export const environment: AppEnvironment = {
  production: true,
  name: 'Staging',

  // TODO: set to the Staging Gateway origin, e.g. 'https://gateway.staging.example'.
  // Leave empty only if the web app is served from behind the Gateway.
  apiBaseUrl: '',
};
