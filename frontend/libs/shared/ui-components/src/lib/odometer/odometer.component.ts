import { Component, ChangeDetectionStrategy, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'bb-odometer',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="visually-hidden">{{ value() }}</span>
    <span class="odo-container" [class.odo-still]="!animate()" aria-hidden="true">
      @for (s of slots(); track $index) {
        @if (s.isDigit) {
          <span class="odo-window">
            <span class="odo-drum" [style.--d]="s.d" [style.--k]="s.k" [attr.data-d]="s.d" [attr.data-k]="s.k">
              @for (n of [0,1,2,3,4,5,6,7,8,9]; track n) {
                <span class="odo-face" [style.--n]="n">{{ n }}</span>
              }
            </span>
          </span>
        } @else {
          <span class="odo-static">{{ s.char }}</span>
        }
      }
    </span>
  `,
  styles: [`
    .visually-hidden {
      position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px;
      overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0;
    }
    .odo-container {
      display: inline-flex;
      align-items: baseline;
      line-height: 1.1;
    }
    .odo-window {
      display: block;
      width: 1ch;
      height: 1.1em;
      overflow: hidden;
      perspective: 6em;
      mask-image: linear-gradient(transparent, #000 16%, #000 84%, transparent);
      -webkit-mask-image: linear-gradient(transparent, #000 16%, #000 84%, transparent);
    }
    .odo-drum {
      position: relative;
      display: block;
      height: 1.1em;
      transform-style: preserve-3d;
      transform: translateZ(-1.69em) rotateX(calc(var(--d) * 36deg));
    }
    .odo-face {
      position: absolute;
      inset: 0;
      text-align: center;
      backface-visibility: hidden;
      -webkit-backface-visibility: hidden;
      transform: rotateX(calc(var(--n) * -36deg)) translateZ(1.69em);
    }
    .odo-static {
      display: inline-block;
    }
  `]
})
export class OdometerComponent {
  readonly value = input<string>('');
  readonly animate = input<boolean>(true);

  readonly slots = computed(() => {
    const val = this.value() || '';
    const targetLength = 16;
    let chars = val.split('');
    if (chars.length < targetLength) {
      const pad = new Array(targetLength - chars.length).fill(' ');
      chars = [...pad, ...chars];
    }
    let kIndex = 0;
    return chars.map((char) => {
      const isDigit = /[0-9]/.test(char);
      let d = 0;
      let k = 0;
      if (isDigit) {
        d = parseInt(char, 10);
        k = kIndex++;
      }
      return { char, isDigit, d, k };
    });
  });
}
