import * as Sentry from "@sentry/nextjs";
import { loadAgentConfig } from "@/lib/config";
import { buildCssVariableStyle } from "@/lib/branding";
import { Nav } from "@/components/Nav";
import { Footer } from "@/components/sections";

interface PageProps {
  searchParams: Promise<{ agentId?: string }>;
}

export default async function ThankYouPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;
  const id = agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";

  try {
    const agent = await loadAgentConfig(id);
    const cssVars = buildCssVariableStyle(agent.branding);

    return (
      <div style={cssVars as React.CSSProperties}>
        <Nav agent={agent} />
        <main className="pt-[74px] min-h-[70vh] flex items-center justify-center">
          <div className="text-center max-w-lg px-6">
            <div className="text-6xl mb-6">✓</div>
            <h1 className="text-3xl font-bold mb-3" style={{ color: "var(--color-primary)" }}>
              Thank You!
            </h1>
            <p className="text-lg font-semibold mb-4" style={{ color: "var(--color-accent)" }}>
              Your Free Home Value Report Is Being Prepared Now!
            </p>
            <p className="text-gray-600 mb-6">
              {agent.identity.name} will send your personalized Comparative Market Analysis
              to your email shortly. Keep an eye on your inbox!
            </p>
            <a
              href={`tel:${agent.identity.phone}`}
              className="inline-block px-8 py-3 rounded-full font-bold"
              style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
            >
              Call {agent.identity.name}: {agent.identity.phone}
            </a>
          </div>
        </main>
        <Footer agent={agent} />
      </div>
    );
  } catch (err) {
    Sentry.captureException(err, { tags: { agentId: id } });
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
