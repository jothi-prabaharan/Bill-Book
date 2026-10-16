import { BrowserWindow, Display, app, screen } from 'electron';
import { readFileSync, writeFileSync } from 'node:fs';
import * as path from 'node:path';
import {
  DisplayArea,
  MIN_SIZE,
  Rect,
  centredIn,
  displayFor,
  isReachable,
  parseWindowState,
  restoreBounds,
} from './window-state';

const stateFile = (): string => path.join(app.getPath('userData'), 'window-state.json');

function area(display: Display): DisplayArea {
  return { id: display.id, workArea: display.workArea };
}

function displays(): DisplayArea[] {
  return screen.getAllDisplays().map(area);
}

function readState(): ReturnType<typeof parseWindowState> {
  try {
    return parseWindowState(JSON.parse(readFileSync(stateFile(), 'utf8')));
  } catch {
    return null;
  }
}

/**
 * The main window, placed where it was last left and kept reachable when a
 * monitor is unplugged or rearranged (TK-184).
 */
export function createMainWindow(preload: string, url: string): BrowserWindow {
  const saved = readState();
  const bounds = restoreBounds(saved, displays(), area(screen.getPrimaryDisplay()));
  const icon = process.platform === 'win32'
    ? path.join(__dirname, 'icon.ico')
    : path.join(__dirname, 'icon.png');

  const win = new BrowserWindow({
    ...bounds,
    minWidth: MIN_SIZE.width,
    minHeight: MIN_SIZE.height,
    show: false,
    title: 'Bill Book',
    icon,
    backgroundColor: '#f3f2f2',
    autoHideMenuBar: true,
    webPreferences: {
      preload,
      contextIsolation: true,
      sandbox: true,
      nodeIntegration: false,
      webviewTag: false,
      spellcheck: true,
    },
  });

  if (saved?.maximized) {
    win.maximize();
  }

  if (saved?.fullScreen) {
    win.setFullScreen(true);
  }

  win.once('ready-to-show', () => win.show());
  remember(win);
  keepReachable(win);
  loadWithRetry(win, url);

  return win;
}

/** Saves the window's place, debounced while it is dragged, and once more as it closes. */
function remember(win: BrowserWindow): void {
  let timer: NodeJS.Timeout | undefined;

  const save = (): void => {
    if (win.isDestroyed()) {
      return;
    }

    const bounds = win.getNormalBounds();
    const state = {
      bounds,
      maximized: win.isMaximized(),
      fullScreen: win.isFullScreen(),
      displayId: displayFor(bounds, displays())?.id ?? null,
    };

    try {
      writeFileSync(stateFile(), JSON.stringify(state));
    } catch {
      // A read-only profile: the window simply opens at the default place next time.
    }
  };

  const later = (): void => {
    clearTimeout(timer);
    timer = setTimeout(save, 500);
  };

  win.on('resize', later);
  win.on('move', later);
  win.on('close', () => {
    clearTimeout(timer);
    save();
  });
}

/** Brings the window back onto a screen when the one it was on goes away. */
function keepReachable(win: BrowserWindow): void {
  const rescue = (): void => {
    if (win.isDestroyed()) {
      return;
    }

    const all = displays();
    const current: Rect = win.getNormalBounds();

    if (isReachable(current, all)) {
      return;
    }

    const maximized = win.isMaximized();

    if (maximized) {
      win.unmaximize();
    }

    win.setBounds(centredIn(area(screen.getPrimaryDisplay()).workArea, current));

    if (maximized) {
      win.maximize();
    }
  };

  screen.on('display-removed', rescue);
  screen.on('display-metrics-changed', rescue);
  win.on('closed', () => {
    screen.removeListener('display-removed', rescue);
    screen.removeListener('display-metrics-changed', rescue);
  });
}

/** The dev server may still be compiling when Electron starts; try for a minute. */
function loadWithRetry(win: BrowserWindow, url: string, attempts = 60): void {
  win.loadURL(url).catch(() => {
    if (attempts > 1 && !win.isDestroyed()) {
      setTimeout(() => loadWithRetry(win, url, attempts - 1), 1000);
    }
  });
}
