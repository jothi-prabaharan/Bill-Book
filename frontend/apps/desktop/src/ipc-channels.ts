/** The IPC channels between the preload script and the main process (TK-184). */
export const IPC = {
  info: 'desktop:info',
  manifest: 'update:manifest',
  print: 'desktop:print',
  resume: 'desktop:resume',
  updateDownload: 'update:download',
  updateInstall: 'update:install',
  updateProgress: 'update:progress',
  checkRequested: 'update:check-requested',
} as const;
