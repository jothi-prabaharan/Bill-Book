import { AfterContentInit, ContentChild, Directive, ElementRef, NgZone, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { MotionService } from './motion.service';

@Directive({
  selector: '[bbRouteMotion]',
  standalone: true
})
export class RouteMotionDirective implements AfterContentInit {
  private el = inject(ElementRef);
  private ngZone = inject(NgZone);
  private motionService = inject(MotionService);

  /**
   * The outlet this directive's element holds — `<main bbRouteMotion><router-outlet/></main>`.
   * It is a child, so it is a content query: injecting RouterOutlet instead finds
   * the outlet ABOVE the element, the app's own, which activates once at sign-in
   * and never again, so no page transition ever played (TK-189).
   */
  @ContentChild(RouterOutlet) private childOutlet?: RouterOutlet;

  /** An outlet on the element itself, for `<router-outlet bbRouteMotion>`. */
  private readonly ownOutlet = inject(RouterOutlet, { optional: true, self: true });

  private outlet: RouterOutlet | null = null;

  private lastMotionIndex = -1;
  private lastDepth = -1;

  /**
   * Whether the first page has been shown. A flag of its own, not
   * `lastMotionIndex === -1`: a route with no motionIndex (Settings, Contacts…)
   * reads as -1 too, which made the navigation after it look like the first
   * load and skip its transition (TK-189).
   */
  private initialised = false;

  ngAfterContentInit() {
    this.outlet = this.childOutlet ?? this.ownOutlet;
    if (!this.outlet) return;

    this.outlet.activateEvents.subscribe(() => {
      if (!this.motionService.active()) return;

      const activated = this.outlet?.activatedRoute;
      if (!activated) return;

      let routeData = activated.snapshot.data;
      // Traverse up to find motionIndex if not on leaf
      let curr = activated.snapshot;
      while (curr && curr.data && curr.data['motionIndex'] === undefined && curr.parent) {
        curr = curr.parent;
      }
      if (curr && curr.data['motionIndex'] !== undefined) {
        routeData = curr.data;
      }

      const motionIndex = routeData['motionIndex'] ?? -1;
      const depth = routeData['depth'] ?? 0;

      if (!this.initialised) {
        this.initialised = true;
        this.lastMotionIndex = motionIndex;
        this.lastDepth = depth;
        return; // Initial load, no route animation
      }

      let origin = '50% 50%';
      let fromTransform = 'none';

      if (motionIndex !== this.lastMotionIndex) {
        if (motionIndex > this.lastMotionIndex) {
          origin = '50% 0';
          fromTransform = 'perspective(1600px) translate3d(0,36px,-90px) rotateX(9deg)';
        } else {
          origin = '50% 100%';
          fromTransform = 'perspective(1600px) translate3d(0,-36px,-90px) rotateX(-9deg)';
        }
      } else {
        if (depth >= this.lastDepth) {
          origin = '0 50%';
          fromTransform = 'perspective(1600px) translate3d(34px,0,-70px) rotateY(-11deg)';
        } else {
          origin = '100% 50%';
          fromTransform = 'perspective(1600px) translate3d(-34px,0,-70px) rotateY(11deg)';
        }
      }

      this.lastMotionIndex = motionIndex;
      this.lastDepth = depth;

      this.ngZone.runOutsideAngular(() => {
        // Clear any active tilts by triggering a scroll event (which tilt listens to)
        document.dispatchEvent(new Event('scroll'));

        requestAnimationFrame(() => {
          const container = this.el.nativeElement as HTMLElement;
          const children = Array.from(container.children).filter(c => !c.classList.contains('board')) as HTMLElement[];
          const easing = 'cubic-bezier(.2,.75,.25,1)';

          children.forEach((child, i) => {
            const delay = Math.min(i, 6) * 60;
            child.animate([
              { opacity: 0, transformOrigin: origin, transform: fromTransform },
              { opacity: 1, transformOrigin: origin, transform: 'none' }
            ], { duration: 560, delay, easing, fill: 'backwards' });
          });

          // Scoped to the page under this outlet: the chrome — rail, menus, top
          // bar — never animates on a route change (TK-189).
          const cards = Array.from(container.querySelectorAll('.card:not(.board .card)')).slice(0, 18) as HTMLElement[];
          cards.forEach((card, i) => {
            const delay = 140 + i * 40;
            card.animate([
              { opacity: 0, transformOrigin: '50% 100%', transform: 'perspective(1200px) translate3d(0,18px,0) rotateX(26deg)' },
              { opacity: 1, transformOrigin: '50% 100%', transform: 'none' }
            ], { duration: 600, delay, easing, fill: 'backwards' });
          });

          const rows = Array.from(container.querySelectorAll('tbody tr:not(.board tbody tr)')).slice(0, 24) as HTMLElement[];
          rows.forEach((row, i) => {
            const delay = 200 + i * 24;
            row.animate([
              { opacity: 0, transformOrigin: '50% 0', transform: 'perspective(700px) rotateX(-72deg)' },
              { opacity: 1, transformOrigin: '50% 0', transform: 'none' }
            ], { duration: 460, delay, easing, fill: 'backwards' });
          });

          // Only the breadcrumb's current crumb flips (Shell.dc.html:3059). An
          // unscoped [aria-current="page"] found the active rail item first,
          // since the rail comes first in the DOM, and flipped that instead.
          document.querySelectorAll<HTMLElement>('.crumbs [aria-current="page"]').forEach((crumb) =>
            crumb.animate([
              { opacity: 0, transformOrigin: '50% 0', transform: 'perspective(400px) rotateX(-90deg)' },
              { opacity: 1, transformOrigin: '50% 0', transform: 'none' }
            ], { duration: 420, delay: 60, easing, fill: 'backwards' }));
        });
      });
    });
  }
}
