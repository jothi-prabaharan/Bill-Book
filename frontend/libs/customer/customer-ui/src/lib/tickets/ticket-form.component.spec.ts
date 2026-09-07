import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { TicketFormComponent } from './ticket-form.component';
import { CustomerService } from '@bill-book/customer-core';

/**
 * Constructed rather than rendered.
 *
 * This workspace's Vitest runs without the Angular Vite plugin, so a component
 * with a `templateUrl` cannot be compiled — `TestBed.createComponent` fails
 * with "is not resolved", which is what this spec used to do. Building the
 * class inside an injection context exercises everything a `should create`
 * smoke test was ever asserting: that the constructor runs and every `inject()`
 * in it resolves. Rendering is covered by driving the built app in a browser,
 * per the note in CLAUDE.md.
 */
describe('TicketFormComponent', () => {
  let component: TicketFormComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CustomerService],
    });

    component = TestBed.runInInjectionContext(() => new TicketFormComponent());
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
