import type { TemplateComponent } from "./types";

export type { TemplateProps, TemplateComponent, DefaultContent } from "./types";

type TemplateLoader = () => Promise<TemplateComponent>;

export const TEMPLATE_LOADERS: Record<string, TemplateLoader> = {
  "emerald-classic": () => import("./emerald-classic").then((m) => m.EmeraldClassic),
  "modern-minimal": () => import("./modern-minimal").then((m) => m.ModernMinimal),
  "warm-community": () => import("./warm-community").then((m) => m.WarmCommunity),
  "luxury-estate": () => import("./luxury-estate").then((m) => m.LuxuryEstate),
  "urban-loft": () => import("./urban-loft").then((m) => m.UrbanLoft),
  "new-beginnings": () => import("./new-beginnings").then((m) => m.NewBeginnings),
  "light-luxury": () => import("./light-luxury").then((m) => m.LightLuxury),
  "country-estate": () => import("./country-estate").then((m) => m.CountryEstate),
  "coastal-living": () => import("./coastal-living").then((m) => m.CoastalLiving),
  "commercial": () => import("./commercial").then((m) => m.Commercial),
};

export async function getTemplate(name: string): Promise<TemplateComponent> {
  const loader = TEMPLATE_LOADERS[name] ?? TEMPLATE_LOADERS["emerald-classic"];
  return loader();
}
