const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');
const { printRaw } = require('./printer');

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1024,
    height: 768,
    autoHideMenuBar: true,
    webPreferences: {
      // The page reaches the printer only through preload.js (TK-41).
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js'),
    }
  });

  const isDev = process.env.NODE_ENV && process.env.NODE_ENV.trim() === 'development';
  if (isDev) {
    const loadRetry = (url, maxRetries) => {
      mainWindow.loadURL(url).catch((err) => {
        if (maxRetries > 0) {
          setTimeout(() => loadRetry(url, maxRetries - 1), 1000);
        }
      });
    };
    loadRetry('http://localhost:4201/#/login', 15); // Try for 15 seconds
    // mainWindow.webContents.openDevTools(); // Hidden by default
  } else {
    mainWindow.loadFile(path.join(__dirname, 'index.html'));
  }

  mainWindow.on('closed', function () {
    mainWindow = null;
  });
}

// Receipt bytes from the till (TK-41). A device path is written as a file;
// a network target is raw TCP, which is how port-9100 printers and ESC/POS
// emulators listen. A failure rejects the renderer's promise with its message.
ipcMain.handle('pos:print', (_event, bytes, target) => printRaw(Buffer.from(bytes), target));

app.on('ready', createWindow);

app.on('window-all-closed', function () {
  if (process.platform !== 'darwin') app.quit();
});

app.on('activate', function () {
  if (mainWindow === null) createWindow();
});
