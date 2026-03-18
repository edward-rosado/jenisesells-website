import type { TemplateComponent } from "./types";
import { EmeraldClassic } from "./emerald-classic";
import { ModernMinimal } from "./modern-minimal";
import { WarmCommunity } from "./warm-community";
import { LuxuryEstate } from "./luxury-estate";
import { UrbanLoft } from "./urban-loft";
import { NewBeginnings } from "./new-beginnings";
import { LightLuxury } from "./light-luxury";
import { CountryEstate } from "./country-estate";
import { CoastalLiving } from "./coastal-living";
import { Commercial } from "./commercial";

export type { TemplateProps, TemplateComponent } from "./types";

export const TEMPLATES: Record<string, TemplateComponent> = {
  "emerald-classic": EmeraldClassic,
  "modern-minimal": ModernMinimal,
  "warm-community": WarmCommunity,
  "luxury-estate": LuxuryEstate,
  "urban-loft": UrbanLoft,
  "new-beginnings": NewBeginnings,
  "light-luxury": LightLuxury,
  "country-estate": CountryEstate,
  "coastal-living": CoastalLiving,
  "commercial": Commercial,
};

export function getTemplate(name: string): TemplateComponent {
  return TEMPLATES[name] || EmeraldClassic;
}
