import { EmeraldClassic } from "./emerald-classic";
import { ModernMinimal } from "./modern-minimal";
import { WarmCommunity } from "./warm-community";
import { LuxuryEstate } from "./luxury-estate";

export type { TemplateProps } from "./types";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
  "warm-community": WarmCommunity,
  "luxury-estate": LuxuryEstate,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
