import { DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

/** The query the secondary menu's + adds when a register opens its form in place. */
export const CREATE_ACTION = 'create';

/**
 * Opens a page's create form when the shell asks for one with `?action=create` —
 * the secondary menu's + on a register whose form opens in place rather than at
 * a `/new` route (TK-189). Call it in a constructor or field initialiser.
 *
 * It reacts to the query changing, not only to the first load: the + on the
 * register already on screen changes nothing but the query, so the router keeps
 * the component and a check in `ngOnInit` alone never sees it — which is how the
 * + came to do nothing on every list that relied on one.
 *
 * The request is looked at once the current navigation has settled. If the page
 * has gone, or is already on its way somewhere else — the contacts list, say,
 * passes the request on to `/contacts/new` itself — it is left alone. Otherwise
 * it is taken off the URL, so a reload or Back does not open the form again.
 *
 * `open` then runs as soon as `ready` says the page can take it — pass the same
 * condition that enables the page's own New button (`() => !this.busy()`), so a
 * page opened fresh from the menu does not start its form before its lookups have
 * loaded. `ready` must read signals: it is watched, not polled.
 */
export function onCreateRequest(open: () => void, ready: () => boolean = () => true): void {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const requested = signal(false);
  let destroyed = false;

  const subscription = route.queryParamMap.subscribe((params) => {
    if (params.get('action') !== CREATE_ACTION) return;
    setTimeout(() => {
      const navigating = typeof router.currentNavigation === 'function' ? router.currentNavigation() : null;
      if (destroyed || navigating) return;
      void router.navigate([], {
        relativeTo: route,
        queryParams: { action: null },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
      requested.set(true);
    });
  });

  effect(() => {
    if (!requested() || !ready()) return;
    untracked(() => {
      requested.set(false);
      open();
    });
  });

  inject(DestroyRef).onDestroy(() => {
    destroyed = true;
    subscription.unsubscribe();
  });
}
