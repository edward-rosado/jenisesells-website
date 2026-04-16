import type { AccountConfig, AgentConfig, ContentConfig, PageSections } from "@/features/config/types";

export interface TemplateProps {
  account: AccountConfig;
  content: ContentConfig;
  /** When rendering an agent sub-page, this is the agent's identity */
  agent?: AgentConfig;
  /** BCP 47 locale code (e.g., "en", "es"). Defaults to "en" when omitted. */
  locale?: string;
}

export type TemplateComponent = (props: TemplateProps) => React.JSX.Element;

/** Build a Set of section IDs that are enabled in the content config (for Nav filtering) */
export function getEnabledSections(sections: PageSections): Set<string> {
  const result = new Set<string>();
  for (const [key, section] of Object.entries(sections)) {
    if (section?.enabled) result.add(key);
  }
  return result;
}

/**
 * Fallback content for each section when pipeline-generated or config-provided
 * content is absent. Every template exports a `defaultContent` satisfying this
 * interface — only sections the template actually renders need to be present.
 */
export interface DefaultContent {
  hero?: {
    title?: string;
    subtitle?: string;
    ctaText?: string;
  };
  stats?: {
    title?: string;
  };
  features?: {
    title?: string;
    subtitle?: string;
  };
  steps?: {
    title?: string;
    subtitle?: string;
  };
  gallery?: {
    title?: string;
    subtitle?: string;
  };
  testimonials?: {
    title?: string;
  };
  profiles?: {
    title?: string;
    subtitle?: string;
  };
  contact?: {
    title?: string;
    subtitle?: string;
  };
  about?: {
    title?: string;
    subtitle?: string;
  };
  marquee?: {
    title?: string;
  };
}
