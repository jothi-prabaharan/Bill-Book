import { InjectionToken } from '@angular/core';

export interface MotionConfig {
  enabled: boolean;
  tiltMaxDeg: number;
  texture: 'currency' | 'grid' | 'none';
}

export const MOTION_CONFIG = new InjectionToken<MotionConfig>('MOTION_CONFIG', {
  factory: () => ({ enabled: true, tiltMaxDeg: 6, texture: 'currency' })
});
