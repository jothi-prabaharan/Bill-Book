import { readFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

describe('Design Tokens & Classical Styling System (shared/theming)', () => {
  // Read the primary design tokens and application stylesheets
  const stylesPath = resolve(__dirname, '../../../../../apps/web/src/styles.scss');
  const themingDir = resolve(__dirname, '../lib');
  const themingFiles = [
    '_tokens.scss',
    '_typography.scss',
    '_buttons.scss',
    '_forms.scss',
    '_cards.scss',
    '_tags.scss',
    '_table.scss',
    '_dialog.scss',
    '_utilities.scss',
  ];
  const themingContent = themingFiles
    .map((f) => {
      const p = resolve(themingDir, f);
      return existsSync(p) ? readFileSync(p, 'utf-8') : '';
    })
    .join('\n');
  const webContent = existsSync(stylesPath) ? readFileSync(stylesPath, 'utf-8') : '';
  const fileContent = `${themingContent}\n${webContent}`;

  describe('Tier 1: Feature / Token Coverage (R1 Specification)', () => {
    it('TOK-T1-01: Core color variables are declared on :root', () => {
      expect(fileContent).toContain('--color-bg:');
      expect(fileContent).toContain('--color-surface:');
      expect(fileContent).toContain('--color-text:');
      expect(fileContent).toContain('--color-accent:');
      expect(fileContent).toContain('--color-accent-2:');
      expect(fileContent).toContain('--color-divider:');
    });

    it('TOK-T1-02: Complete Neutral tonal ramp (100 to 900) is defined', () => {
      const neutralSteps = [100, 200, 300, 400, 500, 600, 700, 800, 900];
      for (const step of neutralSteps) {
        expect(fileContent).toContain(`--color-neutral-${step}:`);
      }
    });

    it('TOK-T1-03: Complete Accent and Accent-2 tonal ramps (100 to 900) are defined', () => {
      const steps = [100, 200, 300, 400, 500, 600, 700, 800, 900];
      for (const step of steps) {
        expect(fileContent).toContain(`--color-accent-${step}:`);
        expect(fileContent).toContain(`--color-accent-2-${step}:`);
      }
    });

    it('TOK-T1-04: Typography tokens specify Cormorant Garamond and Lora pairing', () => {
      expect(fileContent).toContain('--font-heading: "Cormorant Garamond"');
      expect(fileContent).toContain('--font-body: "Lora"');
      expect(fileContent).toContain('--font-heading-weight: 600');
    });

    it('TOK-T1-05: Spacing scale follows 4.6px classical base multiplier', () => {
      expect(fileContent).toContain('--space-1: 4.6px');
      expect(fileContent).toContain('--space-2: 9.2px');
      expect(fileContent).toContain('--space-3: 13.8px');
      expect(fileContent).toContain('--space-4: 18.4px');
      expect(fileContent).toContain('--space-6: 27.6px');
      expect(fileContent).toContain('--space-8: 36.8px');
    });

    it('TOK-T1-06: Border radius scale is properly declared', () => {
      expect(fileContent).toContain('--radius-sm: 2px');
      expect(fileContent).toContain('--radius-md: 4px');
      expect(fileContent).toContain('--radius-lg: 7px');
    });

    it('TOK-T1-07: Elevation shadows use whisper drop shadows with color-mix', () => {
      expect(fileContent).toContain('--shadow-sm: 0 1px 2px color-mix(in srgb, #2d2b2b 14%, transparent)');
      expect(fileContent).toContain('--shadow-md: 0 3px 10px color-mix(in srgb, #2d2b2b 16%, transparent)');
      expect(fileContent).toContain('--shadow-lg: 0 12px 32px color-mix(in srgb, #2d2b2b 22%, transparent)');
    });
  });

  describe('Tier 2: Boundary & Design Rule Enforcement', () => {
    it('TOK-T2-01: Buttons use stroke-over-fill (transparent background by default)', () => {
      expect(fileContent).toMatch(/\.btn\s*\{[^}]*background:\s*transparent;/s);
      expect(fileContent).toMatch(/\.btn-primary\s*\{[^}]*color:\s*var\(--color-accent\);[^}]*border-color:\s*var\(--color-accent\);/s);
      expect(fileContent).toMatch(/\.btn-secondary\s*\{[^}]*border-color:\s*var\(--color-divider\);/s);
    });

    it('TOK-T2-02: Cards use transparent background with divider border (stroke-over-fill)', () => {
      expect(fileContent).toMatch(/\.card\s*\{[^}]*background:\s*transparent;[^}]*border:\s*1px solid var\(--color-divider\);/s);
    });

    it('TOK-T2-03: Tabular numerals are enforced for numbers, figures, and KPI cards', () => {
      expect(fileContent).toContain('font-variant-numeric: tabular-nums');
      expect(fileContent).toMatch(/\.tabular-nums\s*\{[^}]*font-variant-numeric:\s*tabular-nums;/s);
      expect(fileContent).toMatch(/\.kpi\s*\{[^}]*font-variant-numeric:\s*tabular-nums;/s);
    });

    it('TOK-T2-04: Themed focus-visible outline is configured with 2px accent stroke', () => {
      expect(fileContent).toMatch(/:focus-visible\s*\{[^}]*outline:\s*2px solid var\(--color-accent\);[^}]*outline-offset:\s*2px;/s);
    });

    it('TOK-T2-05: Selection highlight uses tint mix with accent color', () => {
      expect(fileContent).toMatch(/::selection\s*\{[^}]*background:\s*color-mix\(in srgb, var\(--color-accent\) 30%, transparent\);/s);
    });

    it('TOK-T2-06: Buttons apply active scaling transform for tactile feedback', () => {
      expect(fileContent).toMatch(/\.btn:active:not\(:disabled\)\s*\{[^}]*transform:\s*scale\(0\.97\);/s);
    });
  });

  describe('Tier 3: Component Token Composition & Interaction Rules', () => {
    it('TOK-T3-01: Segmented controls use stroke borders and accent highlight on check', () => {
      expect(fileContent).toMatch(/\.seg\s*\{[^}]*border:\s*1px solid var\(--color-divider\);[^}]*border-radius:\s*var\(--radius-md\);/s);
      expect(fileContent).toMatch(/\.seg-opt:has\(input:checked\)\s*\{[^}]*color:\s*var\(--color-accent\);[^}]*box-shadow:\s*inset 0 0 0 1px var\(--color-accent\);/s);
    });

    it('TOK-T3-02: Tags use tonal background tints with corresponding tonal text', () => {
      expect(fileContent).toMatch(/\.tag-accent\s*\{[^}]*background:\s*var\(--color-accent-100\);[^}]*color:\s*var\(--color-accent-800\);/s);
      expect(fileContent).toMatch(/\.tag-accent-2\s*\{[^}]*background:\s*var\(--color-accent-2-100\);[^}]*color:\s*var\(--color-accent-2-800\);/s);
      expect(fileContent).toMatch(/\.tag-neutral\s*\{[^}]*background:\s*var\(--color-neutral-100\);[^}]*color:\s*var\(--color-neutral-800\);/s);
      expect(fileContent).toMatch(/\.tag-outline\s*\{[^}]*border:\s*1px solid var\(--color-accent\);[^}]*color:\s*var\(--color-accent\);/s);
    });

    it('TOK-T3-03: Dialogs use surface ground and large whisper elevation shadow', () => {
      expect(fileContent).toMatch(/\.dialog\s*\{[^}]*background:\s*var\(--color-surface\);[^}]*box-shadow:\s*var\(--shadow-lg\);/s);
    });
  });

  describe('Tier 4: Table Styling & Dense Layer Ergonomics', () => {
    it('TOK-T4-01: Table headers are sticky with z-index 3 and inset bottom shadow rule', () => {
      expect(fileContent).toMatch(/\.listwrap \.table thead th\s*\{[^}]*position:\s*sticky;[^}]*top:\s*0;[^}]*z-index:\s*var\(--z-table-head\);[^}]*box-shadow:\s*var\(--shadow-table-head\);/s);
    });

    it('TOK-T4-02: Table rows feature hairline divider rules and subtle row hover', () => {
      expect(fileContent).toMatch(/\.table td\s*\{[^}]*border-bottom:\s*1px solid var\(--color-divider\);/s);
      expect(fileContent).toMatch(/\.table tbody tr:hover\s*\{[^}]*background:/s);
    });
  });
});
