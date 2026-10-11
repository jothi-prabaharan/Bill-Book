import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A module's Lucide icon, by the name the menu seed stores (`MenuSeed.cs`), drawn
 * the way the rail draws every icon: stroke 2, round caps, `currentColor`.
 *
 * One switch for the rail, the phone tab bar and the More sheet, so an icon the
 * seed gains needs adding once. A name with no case draws a dashed circle rather
 * than nothing (TK-189: People, Students, Fees, Maintenance, Restaurant and
 * Approvals had no icon at all).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-shell-icon',
  standalone: true,
  templateUrl: './shell-icon.component.html',
  host: { style: 'display: contents' },
})
export class ShellIconComponent {
  readonly name = input<string>('');
  readonly size = input<number>(20);
}
