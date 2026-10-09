import { describe, expect, it } from 'vitest';
import {
  DisplayArea,
  SavedWindowState,
  centredIn,
  defaultSize,
  displayFor,
  isReachable,
  parseWindowState,
  restoreBounds,
} from './window-state';

const laptop: DisplayArea = { id: 1, workArea: { x: 0, y: 0, width: 1440, height: 860 } };
const ultrawide: DisplayArea = { id: 2, workArea: { x: 1440, y: 0, width: 3440, height: 1400 } };

const saved = (over: Partial<SavedWindowState> = {}): SavedWindowState => ({
  bounds: { x: 2000, y: 100, width: 2400, height: 1200 },
  maximized: false,
  fullScreen: false,
  displayId: 2,
  ...over,
});

describe('restoreBounds', () => {
  it('opens where it was left while that monitor is still there', () => {
    expect(restoreBounds(saved(), [laptop, ultrawide], laptop)).toEqual(saved().bounds);
  });

  it('comes back centred on the main screen when its monitor has gone, shrunk to fit', () => {
    expect(restoreBounds(saved(), [laptop], laptop)).toEqual({ x: 0, y: 0, width: 1440, height: 860 });
  });

  it('shrinks a window bigger than the screen it is on', () => {
    const big = saved({ bounds: { x: 0, y: 0, width: 3000, height: 2000 }, displayId: 1 });

    expect(restoreBounds(big, [laptop], laptop)).toEqual({ x: 0, y: 0, width: 1440, height: 860 });
  });

  it('opens a first window at the default size, centred, within 90% of the screen', () => {
    expect(restoreBounds(null, [laptop, ultrawide], laptop)).toEqual({ x: 72, y: 43, width: 1296, height: 774 });
  });

  it('prefers the remembered monitor when the window is off every screen', () => {
    const lost = saved({ bounds: { x: 9000, y: 9000, width: 1200, height: 800 } });

    expect(restoreBounds(lost, [laptop, ultrawide], laptop)).toEqual({ x: 2560, y: 300, width: 1200, height: 800 });
  });
});

describe('isReachable', () => {
  it('needs a grip of the title strip on some screen', () => {
    expect(isReachable({ x: 1400, y: 10, width: 800, height: 600 }, [laptop])).toBe(false);
    expect(isReachable({ x: 1300, y: 10, width: 800, height: 600 }, [laptop])).toBe(true);
    expect(isReachable({ x: 100, y: -500, width: 800, height: 600 }, [laptop])).toBe(false);
  });
});

describe('displayFor', () => {
  it('picks the screen holding most of the window', () => {
    expect(displayFor({ x: 1300, y: 0, width: 1000, height: 600 }, [laptop, ultrawide])?.id).toBe(2);
    expect(displayFor({ x: -5000, y: 0, width: 100, height: 100 }, [laptop, ultrawide])).toBeNull();
  });
});

describe('centredIn and defaultSize', () => {
  it('never goes below the minimum size the phone layout needs', () => {
    expect(centredIn(laptop.workArea, { width: 100, height: 100 })).toEqual({ x: 540, y: 170, width: 360, height: 520 });
  });

  it('caps a first window on an ultrawide at the default size', () => {
    expect(defaultSize(ultrawide.workArea)).toEqual({ width: 1440, height: 900 });
  });
});

describe('parseWindowState', () => {
  it('reads what was saved', () => {
    expect(parseWindowState({ bounds: { x: 1, y: 2, width: 3, height: 4 }, maximized: true, displayId: 7 })).toEqual({
      bounds: { x: 1, y: 2, width: 3, height: 4 },
      maximized: true,
      fullScreen: false,
      displayId: 7,
    });
  });

  it('ignores a damaged file', () => {
    expect(parseWindowState({ bounds: { x: 'a', y: 2, width: 3, height: 4 } })).toBeNull();
    expect(parseWindowState(null)).toBeNull();
    expect(parseWindowState('x')).toBeNull();
  });
});
