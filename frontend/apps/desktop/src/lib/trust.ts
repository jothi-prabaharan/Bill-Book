import type { IpcMainInvokeEvent } from 'electron';
import { APP_HOST, APP_SCHEME } from './app-protocol';

/**
 * Whether a URL belongs to the app: the bundled build on `app://bill-book`, or
 * the dev server in development. Navigation, new windows, permissions and IPC
 * are all refused to anything else.
 */
export function isTrustedUrl(url: string | undefined, devUrl: string | null): boolean {
  if (!url) {
    return false;
  }

  try {
    const parsed = new URL(url);

    // Node gives a custom scheme the origin "null", so the app's is matched by its parts.
    if (parsed.protocol === `${APP_SCHEME}:`) {
      return parsed.host === APP_HOST;
    }

    return devUrl !== null && parsed.origin === new URL(devUrl).origin;
  } catch {
    return false;
  }
}

/** IPC is answered only for the app's own top-level page. */
export function assertTrustedSender(event: IpcMainInvokeEvent, devUrl: string | null): void {
  const frame = event.senderFrame;

  if (!frame || frame !== event.sender.mainFrame || !isTrustedUrl(frame.url, devUrl)) {
    throw new Error('This request did not come from Bill Book.');
  }
}
