import 'zone.js';
import 'zone.js/testing';
import { afterEach } from 'vitest';
import { TestBed, getTestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';

// TestBed needs a platform before anything can be injected out of it. Doing it
// once here rather than per spec file is why this is a setup file.
getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting(), {
  errorOnUnknownElements: true,
  errorOnUnknownProperties: true,
});

// Angular resets TestBed between tests by hooking the test framework's global
// afterEach, which only exists when Vitest runs with `globals: true`. It does
// not here — specs import their own describe/it/expect — so the hook is
// registered explicitly. Without it the first spec's injector leaks into every
// spec after it, and the failure reads as "test module already instantiated"
// rather than as anything to do with configuration.
afterEach(() => {
  TestBed.resetTestingModule();
});
