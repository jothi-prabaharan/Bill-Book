/**
 * What the desktop app's preload script puts on `window.billBookDesktop`
 * (TK-184). The page has no Node and no Electron: these calls are all it can
 * reach, and each one is an IPC request the main process checks.
 */
/**
 * Which printer a page is for. A receipt and a label go silently to the
 * printer chosen for them in the desktop app's Printing menu; a document (an
 * A4 sticker sheet, say) always opens the print dialog.
 */
export type PrintKind = 'receipt' | 'label' | 'document';

export interface DesktopInfo {
  /** The installed desktop version, `app.getVersion()`. */
  readonly version: string;
  /** `win32`, `linux` or `darwin`. */
  readonly os: string;
  /** The `version.json` the desktop app was configured with, if any. */
  readonly updateManifestUrl: string | null;
}

export interface DesktopUpdateResult {
  /** False when the update server has nothing newer than the installed build. */
  readonly available: boolean;
  readonly version: string | null;
}

export interface DesktopBridge {
  info(): Promise<DesktopInfo>;
  /**
   * The configured `version.json`, fetched by the main process: an update
   * server is a plain file server and sends no CORS headers, so the page could
   * not read it itself. Null when no update server is set.
   */
  fetchManifest(): Promise<unknown>;
  /** Prints a whole page on the printer chosen for its kind, silently unless the user turned that off. */
  printHtml(html: string, kind: PrintKind): Promise<void>;
  /** Checks the update server and downloads the new build. Resolves when it is ready to install. */
  downloadUpdate(): Promise<DesktopUpdateResult>;
  /** Quits, installs the downloaded build and starts it again. */
  installUpdate(): Promise<void>;
  /** Download progress in whole percent. Returns the unsubscribe. */
  onDownloadProgress(listener: (percent: number) => void): () => void;
  /** The computer woke from sleep. Returns the unsubscribe. */
  onResume(listener: () => void): () => void;
  /** Help › Check for updates found a newer build. Returns the unsubscribe. */
  onCheckRequested(listener: () => void): () => void;
}

declare global {
  interface Window {
    billBookDesktop?: DesktopBridge;
  }
}

/** The bridge when running inside the desktop app, null in a browser or a phone. */
export function desktopBridge(): DesktopBridge | null {
  try {
    return typeof window === 'undefined' ? null : (window.billBookDesktop ?? null);
  } catch {
    return null;
  }
}
