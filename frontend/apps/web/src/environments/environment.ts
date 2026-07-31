import { AppEnvironment } from './environment.model';

/**
 * Development. This is the file the application imports; the other
 * environment files replace it at build time.
 */
export const environment: AppEnvironment = {
  production: false,
  name: 'Development',
  apiBaseUrl: '',
};
