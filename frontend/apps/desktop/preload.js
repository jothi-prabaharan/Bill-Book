// The one door from the till's page into the operating system (TK-41).
//
// The window runs with context isolation and no Node integration, so the page
// can do exactly what this exposes and nothing else: send receipt bytes to a
// printer target. The work happens in main.js, which owns the file and socket.
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('billBookPrinter', {
  print: (bytes, target) => ipcRenderer.invoke('pos:print', Array.from(bytes), target),
});
