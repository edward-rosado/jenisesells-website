import { loadAgentConfig, loadAgentContent } from "@/lib/config";
import { buildCssVariableStyle } from "@/lib/branding";
import { getTemplate } from "@/templates";

interface PageProps {
  searchParams: Promise<{ agentId?: string }>;
}

export const revalidate = 60; // ISR: revalidate every 60 seconds

export default async function AgentPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;

  // Default to jenise-buckalew for development
  const id = agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";

  try {
    const agent = await loadAgentConfig(id);
    const content = await loadAgentContent(id, agent);

    const cssVars = buildCssVariableStyle(agent.branding);
    const Template = getTemplate(content.template);

    return (
      <div style={cssVars as React.CSSProperties}>
        <Template agent={agent} content={content} />
      </div>
    );
  } catch {
    return (
      <main className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <h1 className="text-4xl font-bold mb-4">Agent Not Found</h1>
          <p className="text-gray-500">No agent site configured for this domain.</p>
        </div>
      </main>
    );
  }
}
