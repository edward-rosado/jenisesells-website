import { EmeraldClassic } from "./emerald-classic";
import { ModernMinimal } from "./modern-minimal";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
