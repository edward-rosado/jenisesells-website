import type { AgentBranding } from "./types";

const DEFAULTS: Required<AgentBranding> = {
  primary_color: "#1B5E20",
  secondary_color: "#2E7D32",
  accent_color: "#C8A951",
  font_family: "Segoe UI",
};

const VALID_HEX_COLOR = /^#[0-9A-Fa-f]{6}$/;
const SAFE_FONT_FAMILY = /^[a-zA-Z0-9 ,\-]+$/;

function safeColor(value: string | undefined, fallback: string): string {
  return value && VALID_HEX_COLOR.test(value) ? value : fallback;
}

function safeFontFamily(value: string | undefined, fallback: string): string {
  return value && SAFE_FONT_FAMILY.test(value) ? value : fallback;
}

export function buildCssVariableStyle(branding: AgentBranding): Record<string, string> {
  const primary = safeColor(branding.primary_color, DEFAULTS.primary_color);
  const secondary = safeColor(branding.secondary_color, DEFAULTS.secondary_color);
  const accent = safeColor(branding.accent_color, DEFAULTS.accent_color);
  const font = safeFontFamily(branding.font_family, DEFAULTS.font_family);
  return {
    "--color-primary": primary,
    "--color-secondary": secondary,
    "--color-accent": accent,
    "--font-family": `'${font}'`,
  };
}
