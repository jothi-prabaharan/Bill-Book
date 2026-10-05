import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ScanComponent, isEditable } from './scan.component';

interface ScanHarness {
  text: { set(value: string): void; (): string };
  message: () => string | null;
  cameraOpen: () => boolean;
  onKeyDown(event: KeyboardEvent): void;
  handleGlobalKey(event: KeyboardEvent): void;
}

function key(key: string, timeStamp: number, target: EventTarget | null = document.body): KeyboardEvent {
  return { key, timeStamp, target, preventDefault: vi.fn() } as unknown as KeyboardEvent;
}

describe('ScanComponent (TK-160)', () => {
  let scan: ScanComponent;
  let harness: ScanHarness;
  let emitted: string[];

  beforeEach(() => {
    TestBed.configureTestingModule({});
    scan = TestBed.runInInjectionContext(() => new ScanComponent());
    harness = scan as unknown as ScanHarness;
    emitted = [];
    scan.scanned.subscribe((code) => emitted.push(code));
  });

  afterEach(() => {
    scan.ngOnDestroy();
    vi.restoreAllMocks();
  });

  it('emits what was typed or scanned into the field on Enter, trimmed, and clears it', () => {
    harness.text.set('  8901234567890 ');
    harness.onKeyDown(key('Enter', 0));

    expect(emitted).toEqual(['8901234567890']);
    expect(harness.text()).toBe('');
  });

  it('leaves the code in the field when clearOnScan is off', () => {
    vi.spyOn(scan, 'clearOnScan').mockReturnValue(false);
    harness.text.set(' INV-0042 ');
    harness.onKeyDown(key('Enter', 0));

    expect(emitted).toEqual(['INV-0042']);
    expect(harness.text()).toBe('INV-0042');
  });

  it('emits nothing for an empty Enter', () => {
    harness.onKeyDown(key('Enter', 0));
    expect(emitted).toEqual([]);
  });

  it('ignores a page-level burst unless global is on', () => {
    for (const [i, k] of [...'12345', 'Enter'].entries()) {
      harness.handleGlobalKey(key(k, i * 5));
    }
    expect(emitted).toEqual([]);
  });

  it('with global on, takes a scanner burst aimed at the page', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const fixture = TestBed.runInInjectionContext(() => new ScanComponent());
    const codes: string[] = [];
    fixture.scanned.subscribe((code) => codes.push(code));
    vi.spyOn(fixture, 'global').mockReturnValue(true);
    const h = fixture as unknown as ScanHarness;

    for (const [i, k] of [...'ABCD1234', 'Enter'].entries()) {
      h.handleGlobalKey(key(k, 1000 + i * 5));
    }

    expect(codes).toEqual(['ABCD1234']);
    fixture.ngOnDestroy();
  });

  it('leaves keys typed into another field to that field', () => {
    vi.spyOn(scan, 'global').mockReturnValue(true);
    const other = document.createElement('input');

    for (const [i, k] of [...'ABCD1234', 'Enter'].entries()) {
      harness.handleGlobalKey(key(k, i * 5, other));
    }

    expect(emitted).toEqual([]);
  });

  it('says the camera needs https on an insecure address, and stays closed', async () => {
    vi.spyOn(window, 'isSecureContext', 'get').mockReturnValue(false);

    await scan.openCamera();

    expect(harness.message()).toContain('https');
    expect(harness.cameraOpen()).toBe(false);
  });

  it('says in one line when permission is denied, and stays closed', async () => {
    vi.spyOn(window, 'isSecureContext', 'get').mockReturnValue(true);
    const denied = Object.assign(new Error('denied'), { name: 'NotAllowedError' });
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia: vi.fn().mockRejectedValue(denied) },
    });

    await scan.openCamera();

    expect(harness.message()).toBe('Camera permission was denied. Type or scan the code instead.');
    expect(harness.cameraOpen()).toBe(false);
  });

  it('stops every camera track on close', () => {
    const stop = vi.fn();
    (scan as unknown as { stream: MediaStream }).stream = { getTracks: () => [{ stop }] } as unknown as MediaStream;

    scan.closeCamera();

    expect(stop).toHaveBeenCalled();
    expect(harness.cameraOpen()).toBe(false);
  });
});

describe('isEditable', () => {
  it('knows a field from the page', () => {
    expect(isEditable(document.createElement('input'))).toBe(true);
    expect(isEditable(document.createElement('textarea'))).toBe(true);
    expect(isEditable(document.createElement('select'))).toBe(true);
    expect(isEditable(document.body)).toBe(false);
    expect(isEditable(null)).toBe(false);
  });
});
