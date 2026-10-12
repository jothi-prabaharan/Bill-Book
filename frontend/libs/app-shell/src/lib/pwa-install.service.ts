import { Injectable, signal } from '@angular/core';

export interface BeforeInstallPromptEvent extends Event {
  readonly platforms: string[];
  readonly userChoice: Promise<{
    outcome: 'accepted' | 'dismissed';
    platform: string;
  }>;
  prompt(): Promise<void>;
}

/**
 * Service to manage Progressive Web App (PWA) installation lifecycle.
 *
 * Captures Chromium's `beforeinstallprompt` event, provides a reactive `canInstall` signal,
 * and enables direct in-app installation prompts from the top bar or navigation drawer.
 */
@Injectable({
  providedIn: 'root',
})
export class PwaInstallService {
  private deferredPrompt: BeforeInstallPromptEvent | null = null;

  /** True when the browser supports installation and the prompt is available. */
  readonly canInstall = signal<boolean>(false);

  /** True when the application is running as an installed standalone PWA. */
  readonly isInstalled = signal<boolean>(false);

  constructor() {
    this.checkStandalone();
    this.listenForInstallEvents();
  }

  private checkStandalone(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const isStandalone =
      window.matchMedia?.('(display-mode: standalone)').matches ||
      (window.navigator as unknown as { standalone?: boolean })?.standalone === true;

    if (isStandalone) {
      this.isInstalled.set(true);
      this.canInstall.set(false);
    }
  }

  private listenForInstallEvents(): void {
    if (typeof window === 'undefined') {
      return;
    }

    window.addEventListener('beforeinstallprompt', (event: Event) => {
      // Prevent browser mini-infobar default so the application can trigger it gracefully
      event.preventDefault();
      this.deferredPrompt = event as BeforeInstallPromptEvent;
      this.canInstall.set(true);
    });

    window.addEventListener('appinstalled', () => {
      this.deferredPrompt = null;
      this.canInstall.set(false);
      this.isInstalled.set(true);
    });
  }

  /**
   * Triggers the native browser install prompt dialog.
   * Returns true if user accepted installation, false otherwise.
   */
  async promptInstall(): Promise<boolean> {
    if (!this.deferredPrompt) {
      return false;
    }

    try {
      await this.deferredPrompt.prompt();
      const choice = await this.deferredPrompt.userChoice;
      if (choice.outcome === 'accepted') {
        this.canInstall.set(false);
        this.isInstalled.set(true);
        this.deferredPrompt = null;
        return true;
      }
      return false;
    } catch (err) {
      console.warn('[PWA] Error launching install prompt:', err);
      return false;
    }
  }
}
