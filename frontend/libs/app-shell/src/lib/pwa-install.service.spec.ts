import { TestBed } from '@angular/core/testing';
import { describe, expect, it, beforeEach, vi, afterEach } from 'vitest';
import { PwaInstallService, BeforeInstallPromptEvent } from './pwa-install.service';

describe('PwaInstallService', () => {
  let service: PwaInstallService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PwaInstallService],
    });
    service = TestBed.inject(PwaInstallService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('initializes with canInstall false when no prompt event has fired', () => {
    expect(service.canInstall()).toBe(false);
  });

  it('captures beforeinstallprompt event and sets canInstall to true', () => {
    const mockEvent = new Event('beforeinstallprompt') as BeforeInstallPromptEvent;
    const preventDefaultSpy = vi.spyOn(mockEvent, 'preventDefault');

    window.dispatchEvent(mockEvent);

    expect(preventDefaultSpy).toHaveBeenCalled();
    expect(service.canInstall()).toBe(true);
  });

  it('handles promptInstall when accepted', async () => {
    const mockEvent = new Event('beforeinstallprompt') as unknown as {
      preventDefault: () => void;
      prompt: () => Promise<void>;
      userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
    };
    mockEvent.preventDefault = vi.fn();
    mockEvent.prompt = vi.fn().mockResolvedValue(undefined);
    mockEvent.userChoice = Promise.resolve({ outcome: 'accepted', platform: 'web' });

    window.dispatchEvent(mockEvent as unknown as Event);
    expect(service.canInstall()).toBe(true);

    const result = await service.promptInstall();
    expect(result).toBe(true);
    expect(service.isInstalled()).toBe(true);
    expect(service.canInstall()).toBe(false);
  });

  it('handles promptInstall when dismissed', async () => {
    const mockEvent = new Event('beforeinstallprompt') as unknown as {
      preventDefault: () => void;
      prompt: () => Promise<void>;
      userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>;
    };
    mockEvent.preventDefault = vi.fn();
    mockEvent.prompt = vi.fn().mockResolvedValue(undefined);
    mockEvent.userChoice = Promise.resolve({ outcome: 'dismissed', platform: 'web' });

    window.dispatchEvent(mockEvent as unknown as Event);
    expect(service.canInstall()).toBe(true);

    const result = await service.promptInstall();
    expect(result).toBe(false);
    expect(service.isInstalled()).toBe(false);
  });

  it('marks isInstalled true and clears canInstall when appinstalled event fires', () => {
    const promptEvent = new Event('beforeinstallprompt') as BeforeInstallPromptEvent;
    vi.spyOn(promptEvent, 'preventDefault');
    window.dispatchEvent(promptEvent);
    expect(service.canInstall()).toBe(true);

    const installedEvent = new Event('appinstalled');
    window.dispatchEvent(installedEvent);

    expect(service.isInstalled()).toBe(true);
    expect(service.canInstall()).toBe(false);
  });

  it('returns false from promptInstall if no prompt is available', async () => {
    const result = await service.promptInstall();
    expect(result).toBe(false);
  });
});
