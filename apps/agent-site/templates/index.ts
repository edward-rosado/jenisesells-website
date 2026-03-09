import { EmeraldClassic } from "./emerald-classic";

export const TEMPLATES: Record<string, typeof EmeraldClassic> = {
  "emerald-classic": EmeraldClassic,
};

export function getTemplate(name: string) {
  return TEMPLATES[name] || EmeraldClassic;
}
