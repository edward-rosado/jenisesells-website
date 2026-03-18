import type { AccountConfig, AgentConfig, ContentConfig } from "@/lib/types";

export interface TemplateProps {
  account: AccountConfig;
  content: ContentConfig;
  /** When rendering an agent sub-page, this is the agent's identity */
  agent?: AgentConfig;
}

export type TemplateComponent = (props: TemplateProps) => React.JSX.Element;

/** Build a Set of section IDs that are enabled in the content config (for Nav filtering) */
export function getEnabledSections(sections: Record<string, { enabled?: boolean } | undefined>): Set<string> {
  const result = new Set<string>();
  for (const [key, section] of Object.entries(sections)) {
    if (section?.enabled) result.add(key);
  }
  return result;
}
