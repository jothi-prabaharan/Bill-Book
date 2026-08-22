import { describe, it, expect } from 'vitest';
import { TOKENS, CSS_VARS, THEME_PALETTE, LAYOUT_LAYERS, BREAKPOINTS } from '../index';

describe('Design Tokens Contract (TypeScript Exports)', () => {
  describe('Color Palettes & Ramps', () => {
    it('should define core palette with stroke-over-fill accent', () => {
      expect(TOKENS.colors.bg).toBe('#f3f2f2');
      expect(TOKENS.colors.surface).toBe('#eae9e9');
      expect(TOKENS.colors.text).toBe('#201f1d');
      expect(TOKENS.colors.ink).toBe('#2f353f');
      expect(TOKENS.colors.accent).toBe('#f06311');
      expect(TOKENS.colors.accent2).toBe('#ac803e');
    });

    it('should define complete 100-900 OKLCH neutral ramp', () => {
      expect(TOKENS.colors.neutral[100]).toBe('#f8f4f4');
      expect(TOKENS.colors.neutral[200]).toBe('#eae7e7');
      expect(TOKENS.colors.neutral[300]).toBe('#d7d3d3');
      expect(TOKENS.colors.neutral[400]).toBe('#bab6b6');
      expect(TOKENS.colors.neutral[500]).toBe('#9b9797');
      expect(TOKENS.colors.neutral[600]).toBe('#7d7979');
      expect(TOKENS.colors.neutral[700]).toBe('#605d5d');
      expect(TOKENS.colors.neutral[800]).toBe('#444141');
      expect(TOKENS.colors.neutral[900]).toBe('#2d2b2b');
    });

    it('should define complete 100-900 primary accent ramp', () => {
      expect(TOKENS.colors.accentRamp[100]).toBe('#fdefe4');
      expect(TOKENS.colors.accentRamp[200]).toBe('#ffe3bf');
      expect(TOKENS.colors.accentRamp[300]).toBe('#facb8d');
      expect(TOKENS.colors.accentRamp[400]).toBe('#f7853f');
      expect(TOKENS.colors.accentRamp[500]).toBe('#f06311');
      expect(TOKENS.colors.accentRamp[600]).toBe('#c94d08');
      expect(TOKENS.colors.accentRamp[700]).toBe('#a03d05');
      expect(TOKENS.colors.accentRamp[800]).toBe('#7a2f04');
      expect(TOKENS.colors.accentRamp[900]).toBe('#3a270d');
    });

    it('should define complete 100-900 gold accent-2 ramp', () => {
      expect(TOKENS.colors.accent2Ramp[100]).toBe('#fff3e4');
      expect(TOKENS.colors.accent2Ramp[500]).toBe('#bc8f4e');
      expect(TOKENS.colors.accent2Ramp[900]).toBe('#382810');
    });

    it('should define semantic alert colors', () => {
      expect(TOKENS.colors.alerts.danger).toBe('#a2332a');
      expect(TOKENS.colors.alerts.warning).toBe('#8a5b00');
      expect(TOKENS.colors.alerts.success).toBe('#187a4b');
    });
  });

  describe('Typography & Spacing Scale', () => {
    it('should specify Cormorant Garamond and Lora typography pairing', () => {
      expect(TOKENS.typography.fontHeading).toContain('Cormorant Garamond');
      expect(TOKENS.typography.fontBody).toContain('Lora');
      expect(TOKENS.typography.fontHeadingWeight).toBe(600);
    });

    it('should define compact and classical spacing scale', () => {
      expect(TOKENS.spacing.space1).toBe('3px');
      expect(TOKENS.spacing.space2).toBe('7px');
      expect(TOKENS.spacing.space3).toBe('10px');
      expect(TOKENS.spacing.space4).toBe('13px');
      expect(TOKENS.spacing.space6).toBe('18px');
      expect(TOKENS.spacing.space8).toBe('24px');
      expect(TOKENS.spacing.classical.space1).toBe('4.6px');
      expect(TOKENS.spacing.classical.space8).toBe('36.8px');
    });

    it('should define border radii', () => {
      expect(TOKENS.radii.sm).toBe('2px');
      expect(TOKENS.radii.md).toBe('4px');
      expect(TOKENS.radii.lg).toBe('7px');
      expect(TOKENS.radii.tag).toBe('3px');
      expect(TOKENS.radii.pill).toBe('999px');
    });
  });

  describe('Layer Stacking & Z-Index Discipline', () => {
    it('should enforce strict layer stacking hierarchy', () => {
      expect(TOKENS.zIndex.topbar).toBe(6);
      expect(TOKENS.zIndex.rail).toBe(5);
      expect(TOKENS.zIndex.breadcrumbs).toBe(4);
      expect(TOKENS.zIndex.tableHead).toBe(3);
      expect(TOKENS.zIndex.content).toBe(1);

      expect(TOKENS.zIndex.topbar).toBeGreaterThan(TOKENS.zIndex.rail);
      expect(TOKENS.zIndex.rail).toBeGreaterThan(TOKENS.zIndex.breadcrumbs);
      expect(TOKENS.zIndex.breadcrumbs).toBeGreaterThan(TOKENS.zIndex.tableHead);
      expect(TOKENS.zIndex.tableHead).toBeGreaterThan(TOKENS.zIndex.content);
    });

    it('should export matching LAYOUT_LAYERS and BREAKPOINTS constants', () => {
      expect(LAYOUT_LAYERS.HEADER).toBe(6);
      expect(LAYOUT_LAYERS.RAIL).toBe(5);
      expect(LAYOUT_LAYERS.BREADCRUMB).toBe(4);
      expect(LAYOUT_LAYERS.STICKY_TABLE_HEADER).toBe(3);
      expect(LAYOUT_LAYERS.CONTENT).toBe(1);
      expect(BREAKPOINTS.MOBILE_MAX).toBe(860);
      expect(BREAKPOINTS.DESKTOP_MIN).toBe(861);
    });
  });

  describe('CSS Variables Map & Theme Palette', () => {
    it('should export CSS_VARS dictionary referencing var() functions', () => {
      expect(CSS_VARS.color.bg).toBe('var(--color-bg)');
      expect(CSS_VARS.color.accent).toBe('var(--color-accent)');
      expect(CSS_VARS.zIndex.tableHead).toBe('var(--z-table-head)');
      expect(CSS_VARS.shadow.header).toBe('var(--shadow-header)');
    });

    it('should export THEME_PALETTE constants for canvas/SVG use', () => {
      expect(THEME_PALETTE.accent).toBe('#f06311');
      expect(THEME_PALETTE.ink).toBe('#2f353f');
      expect(THEME_PALETTE.bg).toBe('#f3f2f2');
    });
  });
});
