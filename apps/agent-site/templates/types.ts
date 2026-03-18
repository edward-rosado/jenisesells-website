import type { AgentConfig, AgentContent } from "@/lib/types";

export interface TemplateProps {
  agent: AgentConfig;
  content: AgentContent;
}

export type TemplateComponent = (props: TemplateProps) => React.JSX.Element;
