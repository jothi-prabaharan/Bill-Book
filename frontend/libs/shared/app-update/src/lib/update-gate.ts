import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Route, Routes } from '@angular/router';
import { AppUpdateService } from './app-update.service';

/** Refuses every navigation while a forced update is pending (TK-184). */
export const updateGate: CanActivateFn & CanActivateChildFn = () => !inject(AppUpdateService).isForcedNow();

/**
 * Puts `updateGate` in front of every top-level route and every page beneath
 * one. A redirect cannot carry a guard, and leads to a route that does.
 */
export function withUpdateGate(routes: Routes): Routes {
  return routes.map((route): Route => {
    if (route.redirectTo !== undefined) {
      return route;
    }

    const nested = route.children !== undefined || route.loadChildren !== undefined;

    return {
      ...route,
      canActivate: [updateGate, ...(route.canActivate ?? [])],
      ...(nested ? { canActivateChild: [updateGate, ...(route.canActivateChild ?? [])] } : {}),
    };
  });
}
