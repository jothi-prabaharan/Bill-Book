import { DesktopBridge } from '@bill-book/app-update';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { printHtml } from './print-html';

const frames = (): HTMLIFrameElement[] => Array.from(document.querySelectorAll('iframe'));

const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

const desktop = (printer: DesktopBridge['printHtml']): DesktopBridge =>
  ({ printHtml: printer }) as Partial<DesktopBridge> as DesktopBridge;

describe('printHtml', () => {
  afterEach(() => {
    delete window.billBookDesktop;
    frames().forEach((frame) => frame.remove());
  });

  it('prints through a hidden frame in a browser', () => {
    printHtml('<p>receipt</p>');

    expect(frames()).toHaveLength(1);
    expect(frames()[0].srcdoc).toBe('<p>receipt</p>');
    expect(frames()[0].getAttribute('aria-hidden')).toBe('true');
  });

  it('hands the page to the desktop app with its kind, and opens no frame (TK-184)', () => {
    const print = vi.fn().mockResolvedValue(undefined);
    window.billBookDesktop = desktop(print);

    printHtml('<p>label</p>', 'label');

    expect(print).toHaveBeenCalledWith('<p>label</p>', 'label');
    expect(frames()).toHaveLength(0);
  });

  it('treats a page with no kind as a receipt', () => {
    const print = vi.fn().mockResolvedValue(undefined);
    window.billBookDesktop = desktop(print);

    printHtml('<p>ticket</p>');

    expect(print).toHaveBeenCalledWith('<p>ticket</p>', 'receipt');
  });

  it('falls back to the print dialog when the desktop printer refuses', async () => {
    window.billBookDesktop = desktop(vi.fn().mockRejectedValue(new Error('Invalid deviceName provided')));

    printHtml('<p>receipt</p>');
    await settle();

    expect(frames()).toHaveLength(1);
  });
});
