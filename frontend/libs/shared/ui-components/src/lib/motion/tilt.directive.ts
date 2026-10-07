import { Directive, ElementRef, NgZone, OnDestroy, OnInit, inject } from '@angular/core';
import { MotionService } from './motion.service';

@Directive({
  selector: '[bbTilt]',
  standalone: true
})
export class TiltDirective implements OnInit, OnDestroy {
  private el = inject(ElementRef);
  private ngZone = inject(NgZone);
  private motionService = inject(MotionService);
  
  private destroyFn?: () => void;

  ngOnInit() {
    this.ngZone.runOutsideAngular(() => {
      let activeCard: HTMLElement | null = null;
      let cachedRect: DOMRect | null = null;
      let ticking = false;
      const eligibleCards = new WeakMap<HTMLElement, boolean>();

      const clearTilt = () => {
        if (activeCard) {
          activeCard.classList.remove('tilt');
          activeCard.style.removeProperty('--rx');
          activeCard.style.removeProperty('--ry');
          activeCard.style.removeProperty('--gx');
          activeCard.style.removeProperty('--gy');
          activeCard = null;
          cachedRect = null;
        }
      };

      const handlePointerMove = (e: PointerEvent) => {
        if (!this.motionService.active() || e.pointerType === 'touch' || window.innerWidth < 860) {
          return;
        }
        
        // If customize mode is on or menu open, skip
        if (document.querySelector('.customize-mode') || document.querySelector('.menu-open')) {
          clearTilt();
          return;
        }

        const target = e.target as HTMLElement;
        const card = target.closest('main .card') as HTMLElement;
        
        if (!card) {
          clearTilt();
          return;
        }

        if (!eligibleCards.has(card)) {
          const isDashboard = card.closest('.board') !== null;
          const isSmallEnough = card.offsetWidth <= 760;
          const hasInputs = card.querySelector('input, select, textarea, [contenteditable]') !== null;
          const inDialog = card.closest('[role="dialog"], .dialog') !== null;
          
          eligibleCards.set(card, isDashboard || (isSmallEnough && !hasInputs && !inDialog));
        }

        if (!eligibleCards.get(card)) {
          clearTilt();
          return;
        }

        if (activeCard !== card) {
          clearTilt();
          activeCard = card;
          cachedRect = card.getBoundingClientRect();
        }

        if (!cachedRect) return;

        if (!ticking) {
          requestAnimationFrame(() => {
            if (activeCard && cachedRect) {
              const px = Math.max(0, Math.min(1, (e.clientX - cachedRect.left) / cachedRect.width));
              const py = Math.max(0, Math.min(1, (e.clientY - cachedRect.top) / cachedRect.height));
              
              const deg = this.motionService.getConfig().tiltMaxDeg;
              const rx = (py - 0.5) * 2 * deg * Math.min(1, 240 / cachedRect.height);
              const ry = (0.5 - px) * 2 * deg * Math.min(1, 320 / cachedRect.width);
              
              activeCard.style.setProperty('--rx', `${rx}deg`);
              activeCard.style.setProperty('--ry', `${ry}deg`);
              activeCard.style.setProperty('--gx', `${px * 100}%`);
              activeCard.style.setProperty('--gy', `${py * 100}%`);
              activeCard.classList.add('tilt');
            }
            ticking = false;
          });
          ticking = true;
        }
      };

      const handleScroll = () => clearTilt();
      const handlePointerOut = (e: PointerEvent) => {
        if (e.relatedTarget === null) clearTilt();
      };
      
      document.addEventListener('pointermove', handlePointerMove, { passive: true });
      document.addEventListener('scroll', handleScroll, { passive: true, capture: true });
      document.addEventListener('pointerout', handlePointerOut, { passive: true });

      this.destroyFn = () => {
        document.removeEventListener('pointermove', handlePointerMove);
        document.removeEventListener('scroll', handleScroll, { capture: true });
        document.removeEventListener('pointerout', handlePointerOut);
        clearTilt();
      };
    });
  }

  ngOnDestroy() {
    if (this.destroyFn) this.destroyFn();
  }
}
