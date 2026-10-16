export type AppClient = 'electron' | 'ionic' | 'web';

/**
 * Resolves the client runtime:
 * - 'electron' when running in the Electron desktop shell (TK-184)
 * - 'ionic' when running in the Capacitor/Ionic mobile or tablet shell (TK-184)
 * - 'web' when running in standard browser/PWA
 */
export function resolveAppClient(): AppClient {
  if (typeof window === 'undefined') {
    return 'web';
  }

  const win = window as unknown as {
    billBookDesktop?: unknown;
    Capacitor?: { isNativePlatform?: () => boolean };
  };

  // 1. Electron desktop bridge or user-agent
  if (win.billBookDesktop != null || /electron/i.test(navigator.userAgent)) {
    return 'electron';
  }

  // 2. Ionic / Capacitor mobile wrapper
  if (win.Capacitor?.isNativePlatform?.() || /capacitor/i.test(navigator.userAgent)) {
    return 'ionic';
  }

  // 3. Web application
  return 'web';
}

/**
 * Resolves the client platform/browser/OS:
 * - 'win', 'mac', 'ubuntu' for desktop operating systems
 * - 'android', 'ios' for mobile operating systems
 * - 'chrome', 'edge' for web browsers
 */
export function resolveAppPlatform(client: AppClient = resolveAppClient()): string {
  if (typeof window === 'undefined') {
    return 'web';
  }

  const ua = navigator.userAgent.toLowerCase();

  // 1. Inside Electron desktop shell: report underlying OS
  if (client === 'electron') {
    if (ua.includes('windows') || ua.includes('win32') || ua.includes('win64')) {
      return 'win';
    }
    if (ua.includes('macintosh') || ua.includes('mac os') || ua.includes('darwin')) {
      return 'mac';
    }
    if (ua.includes('ubuntu') || ua.includes('linux')) {
      return 'ubuntu';
    }
    return 'win';
  }

  // 2. Inside Ionic / Capacitor mobile shell: report mobile OS
  if (client === 'ionic') {
    if (ua.includes('android')) {
      return 'android';
    }
    if (ua.includes('iphone') || ua.includes('ipad') || ua.includes('ipod') || ua.includes('ios')) {
      return 'ios';
    }
    return 'android';
  }

  // 3. Inside Web Browser:
  // Edge browser
  if (ua.includes('edg/') || ua.includes('edge/')) {
    return 'edge';
  }

  // Android device / browser
  if (ua.includes('android')) {
    return 'android';
  }

  // iOS device / browser
  if (ua.includes('iphone') || ua.includes('ipad') || ua.includes('ipod')) {
    return 'ios';
  }

  // Chrome browser
  if (ua.includes('chrome/') || ua.includes('crios/')) {
    return 'chrome';
  }

  // Mac desktop browser (Safari, Firefox, etc.)
  if (ua.includes('macintosh') || ua.includes('mac os')) {
    return 'mac';
  }

  // Linux / Ubuntu desktop browser
  if (ua.includes('ubuntu') || ua.includes('linux')) {
    return 'ubuntu';
  }

  // Windows desktop browser
  if (ua.includes('windows') || ua.includes('win')) {
    return 'win';
  }

  return 'chrome';
}

export const APP_HEADER = 'X-App';
export const PLATFORM_HEADER = 'X-Platform';
