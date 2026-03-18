import type { AccountConfig, AgentConfig, ContentConfig } from "@/lib/types";

export interface TemplateProps {
  account: AccountConfig;
  content: ContentConfig;
  /** When rendering an agent sub-page, this is the agent's identity */
  agent?: AgentConfig;
}

export type TemplateComponent = (props: TemplateProps) => React.JSX.Element;
