import { describe, expect, it } from 'vitest';
import {
  CAMERA_POPUP,
  ScanGate,
  SCAN_FORMATS,
  cameraErrorMessage,
  isScanIdle,
  normaliseScan,
} from './scan-session';

describe('ScanGate', () => {
  it('takes the first read and pauses briefly after it', () => {
    const gate = new ScanGate(800, 2500);
    expect(gate.accept('A1', 0)).toBe(true);
    expect(gate.accept('B2', 500)).toBe(false);
    expect(gate.accept('B2', 900)).toBe(true);
  });

  it('does not take the same code twice while it is held in front of the camera', () => {
    const gate = new ScanGate(800, 2500);
    expect(gate.accept('A1', 0)).toBe(true);
    expect(gate.accept('A1', 1000)).toBe(false);
    expect(gate.accept('A1', 2600)).toBe(true);
  });

  it('ignores an empty read and trims what it takes', () => {
    const gate = new ScanGate();
    expect(gate.accept('  ', 0)).toBe(false);
    expect(gate.accept(' A1\r\n', 0)).toBe(true);
    expect(gate.accept('A1', 1000)).toBe(false);
  });

  it('forgets the last code after reset', () => {
    const gate = new ScanGate();
    gate.accept('A1', 0);
    gate.reset();
    expect(gate.accept('A1', 10)).toBe(true);
  });
});

describe('scan session rules', () => {
  it('keeps the popup small, never full screen', () => {
    expect(CAMERA_POPUP.width).toBeLessThanOrEqual(160);
    expect(CAMERA_POPUP.height).toBeLessThanOrEqual(120);
  });

  it('reads retail barcodes and QR codes', () => {
    expect(SCAN_FORMATS).toContain('ean_13');
    expect(SCAN_FORMATS).toContain('code_128');
    expect(SCAN_FORMATS).toContain('qr_code');
  });

  it('closes after the idle period and not before', () => {
    expect(isScanIdle(0, 29_999, 30_000)).toBe(false);
    expect(isScanIdle(0, 30_000, 30_000)).toBe(true);
  });

  it('trims what a reader adds around a code', () => {
    expect(normaliseScan('\n 890123 \r')).toBe('890123');
    expect(normaliseScan(null)).toBe('');
  });

  it('says in one line why the camera did not start, and that typing still works', () => {
    expect(cameraErrorMessage('NotAllowedError')).toBe('Camera permission was denied. Type or scan the code instead.');
    expect(cameraErrorMessage('NotFoundError')).toContain('No camera');
    expect(cameraErrorMessage(null, false)).toContain('https');
    for (const name of ['NotAllowedError', 'NotFoundError', 'NotReadableError', 'Unsupported', 'Weird']) {
      expect(cameraErrorMessage(name)).toContain('Type or scan the code instead.');
      expect(cameraErrorMessage(name)).not.toContain('\n');
    }
  });
});
