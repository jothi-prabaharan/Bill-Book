import { Injectable, computed, signal } from '@angular/core';

const STORAGE_KEY = 'billbook.favourites.v1';

/**
 * The screens a person has starred, kept in their own browser.
 *
 * There is no favourites endpoint behind the product yet, so this is deliberately
 * device-local: the list survives a reload on this machine and travels nowhere
 * else. When a server-side list arrives, this service is the only thing that has
 * to change — nothing reads `localStorage` directly.
 */
@Injectable({ providedIn: 'root' })
export class FavouritesService {
  private readonly paths = signal<readonly string[]>(read());

  /** Starred paths, in the order they were starred. */
  readonly starred = computed(() => this.paths());

  readonly count = computed(() => this.paths().length);

  isStarred(path: string): boolean {
    return this.paths().includes(path);
  }

  star(path: string): void {
    if (this.isStarred(path)) return;
    this.write([...this.paths(), path]);
  }

  unstar(path: string): void {
    this.write(this.paths().filter((p) => p !== path));
  }

  toggle(path: string): void {
    if (this.isStarred(path)) {
      this.unstar(path);
    } else {
      this.star(path);
    }
  }

  private write(next: readonly string[]): void {
    this.paths.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // A private window, or storage the browser has blocked. The list still
      // works for this session; it just will not survive a reload.
    }
  }
}

/**
 * Read the stored list, treating anything that is not an array of strings as
 * absent rather than trusting it — the key is writable by anything running on
 * this origin.
 */
function read(): readonly string[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((p): p is string => typeof p === 'string');
  } catch {
    return [];
  }
}
