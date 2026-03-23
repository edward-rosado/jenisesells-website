// apps/agent-site/features/config/nav-config.ts
// Lightweight nav data loader — does NOT import the full content registry.
// Use this in legal pages to avoid pulling content data into those route chunks.
import type { NavigationConfig } from "./types";
import { navData } from "./nav-registry";

export function loadNavConfig(handle: string): {
  navigation?: NavigationConfig;
  enabledSections: Set<string>;
} {
  const entry = navData[handle];
  if (!entry) return { navigation: undefined, enabledSections: new Set() };
  return {
    navigation: entry.navigation,
    enabledSections: new Set(entry.enabledSections),
  };
}
