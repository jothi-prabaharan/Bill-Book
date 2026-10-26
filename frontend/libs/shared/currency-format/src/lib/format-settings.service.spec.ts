import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { DEFAULT_FORMAT_SETTINGS, FormatSettings } from './format-settings';
import { FormatSettingsService } from './format-settings.service';

/** The amount with no symbol, for a figure in another currency (TK-262). */
describe('FormatSettingsService.formatAmount', () => {
  const western: FormatSettings = { ...DEFAULT_FORMAT_SETTINGS, currencyCode: 'USD', currencySymbol: '$', currencyMask: '###,###,##0.00', currencyDecimals: 3 };

  function setup(settings: FormatSettings = western) {
    TestBed.configureTestingModule({
      providers: [FormatSettingsService, { provide: HttpClient, useValue: { get: () => of(settings) } }],
    });

    return TestBed.inject(FormatSettingsService);
  }

  it('FS-01: groups and rounds as the branch does, with no symbol, once its formats have loaded', async () => {
    const formats = setup();

    // Before the answer the shipped defaults stand: rupee grouping, two decimals.
    expect(formats.formatAmount(1234567.5)).toBe('12,34,567.50');

    await formats.load();

    expect(formats.formatAmount(1234567.5)).toBe('1,234,567.500');
    expect(formats.formatAmount(null)).toBe('');
  });
});
