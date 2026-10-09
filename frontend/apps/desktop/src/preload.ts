// The only door from the page into the desktop (TK-184). The window runs with
// context isolation, a sandbox and no Node, so the page can do exactly what is
// exposed here; each call is an IPC request the main process checks.
import { contextBridge, ipcRenderer, IpcRendererEvent } from 'electron';
import type { DesktopBridge, DesktopInfo, DesktopUpdateResult, PrintKind } from '@bill-book/app-update';
import { IPC } from './ipc-channels';

/** IPC wraps a main-process error in "Error invoking remote method …"; the page wants the sentence. */
function invoke<T>(channel: string, ...args: unknown[]): Promise<T> {
  return ipcRenderer.invoke(channel, ...args).catch((error: unknown) => {
    const text = error instanceof Error ? error.message : String(error);
    throw new Error(text.replace(/^Error invoking remote method '[^']+': (?:Error: )?/, ''));
  }) as Promise<T>;
}

function subscribe<T extends unknown[]>(channel: string, listener: (...args: T) => void): () => void {
  const relay = (_event: IpcRendererEvent, ...args: unknown[]): void => listener(...(args as T));
  ipcRenderer.on(channel, relay);
  return () => {
    ipcRenderer.removeListener(channel, relay);
  };
}

const bridge: DesktopBridge = {
  info: () => invoke<DesktopInfo>(IPC.info),
  fetchManifest: () => invoke<unknown>(IPC.manifest),
  printHtml: (html: string, kind: PrintKind) => invoke<void>(IPC.print, String(html), String(kind)),
  downloadUpdate: () => invoke<DesktopUpdateResult>(IPC.updateDownload),
  installUpdate: () => invoke<void>(IPC.updateInstall),
  onDownloadProgress: (listener) => subscribe<[number]>(IPC.updateProgress, (percent) => listener(Number(percent))),
  onResume: (listener) => subscribe<[]>(IPC.resume, () => listener()),
  onCheckRequested: (listener) => subscribe<[]>(IPC.checkRequested, () => listener()),
};

contextBridge.exposeInMainWorld('billBookDesktop', bridge);
