import { describe, it, expect } from 'vitest';
import { resolveErpLoginUrl, WindowLocationLike } from './app.routes';

describe('resolveErpLoginUrl', () => {
  describe('Staging subdomains (stg -> stg-erp)', () => {
    it('redirects stg.eternalpathway.in to stg-erp.eternalpathway.in/login', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'stg.eternalpathway.in',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://stg-erp.eternalpathway.in/login');
    });

    it('corrects legacy nested app.stg.eternalpathway.in to stg-erp.eternalpathway.in/login', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'app.stg.eternalpathway.in',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://stg-erp.eternalpathway.in/login');
    });

    it('redirects stg-doc.eternalpathway.in to stg-erp.eternalpathway.in/login', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'stg-doc.eternalpathway.in',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://stg-erp.eternalpathway.in/login');
    });

    it('preserves query params during staging redirect', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'stg.eternalpathway.in',
        port: '',
        search: '?returnUrl=%2Fsales%2Finvoices',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://stg-erp.eternalpathway.in/login?returnUrl=%2Fsales%2Finvoices');
    });
  });

  describe('UAT subdomains (uat -> uat-erp)', () => {
    it('redirects uat.eternalpathway.in to uat-erp.eternalpathway.in/login', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'uat.eternalpathway.in',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://uat-erp.eternalpathway.in/login');
    });
  });

  describe('Production and apex domains', () => {
    it('redirects eternalpathway.in to app.eternalpathway.in/login', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'eternalpathway.in',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://app.eternalpathway.in/login');
    });

    it('redirects www.eternalpathway.in to app.eternalpathway.in/login', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'www.eternalpathway.in',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://app.eternalpathway.in/login');
    });

    it('keeps stg-erp.eternalpathway.in intact when already on ERP host', () => {
      const loc: WindowLocationLike = {
        protocol: 'https:',
        hostname: 'stg-erp.eternalpathway.in',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('https://stg-erp.eternalpathway.in/login');
    });
  });

  describe('Local development and port-based hosting', () => {
    it('maps port 5202 (staging website) to port 5200 (staging ERP web app)', () => {
      const loc: WindowLocationLike = {
        protocol: 'http:',
        hostname: '117.247.103.65',
        port: '5202',
      };
      expect(resolveErpLoginUrl(loc)).toBe('http://117.247.103.65:5200/login');
    });

    it('maps port 4202 (local website) to port 4200 (local ERP web app)', () => {
      const loc: WindowLocationLike = {
        protocol: 'http:',
        hostname: 'localhost',
        port: '4202',
      };
      expect(resolveErpLoginUrl(loc)).toBe('http://localhost:4200/login');
    });

    it('maps default localhost without port to port 4200', () => {
      const loc: WindowLocationLike = {
        protocol: 'http:',
        hostname: 'localhost',
        port: '',
      };
      expect(resolveErpLoginUrl(loc)).toBe('http://localhost:4200/login');
    });
  });
});
