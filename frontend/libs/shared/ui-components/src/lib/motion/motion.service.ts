import { Injectable, NgZone, signal, OnDestroy, inject } from '@angular/core';
import { MOTION_CONFIG } from './motion.config';

@Injectable({ providedIn: 'root' })
export class MotionService implements OnDestroy {
  readonly active = signal<boolean>(false);
  private mql: MediaQueryList;
  private listener: (e: MediaQueryListEvent) => void;
  private config = inject(MOTION_CONFIG);
  private ngZone = inject(NgZone);

  constructor() {
    this.mql = window.matchMedia('(prefers-reduced-motion: reduce)');
    this.updateActive(this.mql.matches);
    
    this.listener = (e) => {
      this.ngZone.run(() => this.updateActive(e.matches));
    };
    this.mql.addEventListener('change', this.listener);
  }

  private updateActive(reduced: boolean) {
    this.active.set(this.config.enabled && !reduced);
  }

  ngOnDestroy() {
    this.mql.removeEventListener('change', this.listener);
  }

  getConfig() { return this.config; }
}
