const { app, BrowserWindow } = require('electron');
const path = require('path');

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1024,
    height: 768,
    autoHideMenuBar: true,
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false // Simplistic setup for demo purposes
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

app.on('ready', createWindow);

app.on('window-all-closed', function () {
  if (process.platform !== 'darwin') app.quit();
});

app.on('activate', function () {
  if (mainWindow === null) createWindow();
});
