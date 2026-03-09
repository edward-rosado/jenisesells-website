import type { AgentBranding } from "./types";

const DEFAULTS: Required<AgentBranding> = {
  primary_color: "#1B5E20",
  secondary_color: "#2E7D32",
  accent_color: "#C8A951",
  font_family: "Segoe UI",
};

export function buildCssVariables(branding: AgentBranding): string {
  const merged = { ...DEFAULTS, ...branding };
  return [
    `--color-primary: ${merged.primary_color}`,
    `--color-secondary: ${merged.secondary_color}`,
    `--color-accent: ${merged.accent_color}`,
    `--font-family: '${merged.font_family}'`,
  ].join("; ");
}
