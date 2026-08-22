import { Injectable } from '@angular/core';
import { GridState } from './data-grid.models';

@Injectable({ providedIn: 'root' })
export class DataGridService {
  private readonly PREFIX = 'bb_grid_state_';

  saveState(state: GridState): void {
    if (!state.gridCode) return;
    try {
      localStorage.setItem(this.PREFIX + state.gridCode, JSON.stringify(state));
    } catch (e) {
      console.warn('Could not save grid state', e);
    }
  }

  loadState(gridCode: string): GridState | null {
    if (!gridCode) return null;
    try {
      const stored = localStorage.getItem(this.PREFIX + gridCode);
      return stored ? JSON.parse(stored) : null;
    } catch (e) {
      console.warn('Could not load grid state', e);
      return null;
    }
  }

  clearState(gridCode: string): void {
    localStorage.removeItem(this.PREFIX + gridCode);
  }
}
