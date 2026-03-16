import { EmeraldClassic } from "./emerald-classic";
import { ModernMinimal } from "./modern-minimal";
import { WarmCommunity } from "./warm-community";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
  "warm-community": WarmCommunity,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
