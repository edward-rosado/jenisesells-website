import type { Metadata } from "next";
import * as Sentry from "@sentry/nextjs";
import { notFound } from "next/navigation";
import { loadAgentConfig, loadAgentContent } from "@/lib/config";
import { buildCssVariableStyle } from "@/lib/branding";
import { getTemplate } from "@/templates";
import { Analytics } from "@/components/Analytics";

interface PageProps {
  searchParams: Promise<{ agentId?: string }>;
}

export const revalidate = 60; // ISR: revalidate every 60 seconds

function resolveAgentId(agentId?: string): string {
  return agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}

export async function generateMetadata({ searchParams }: PageProps): Promise<Metadata> {
  const { agentId } = await searchParams;
  const id = resolveAgentId(agentId);
  try {
    const agent = await loadAgentConfig(id);
    return {
      title: `${agent.identity.name} | ${agent.identity.title ?? "Real Estate Agent"}`,
      description: agent.identity.tagline ?? `${agent.identity.name} — serving ${agent.location.service_areas?.join(", ") ?? agent.location.state}`,
      openGraph: {
        title: agent.identity.name,
        description: agent.identity.tagline ?? "",
        type: "website",
      },
    };
  } catch {
    return { title: "Real Estate Agent" };
  }
}

export default async function AgentPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;
  const id = resolveAgentId(agentId);

  try {
    const agent = await loadAgentConfig(id);
    const content = await loadAgentContent(id, agent);

    const cssVars = buildCssVariableStyle(agent.branding);
    const Template = getTemplate(content.template);

    const jsonLd = {
      "@context": "https://schema.org",
      "@type": "RealEstateAgent",
      name: agent.identity.name,
      telephone: agent.identity.phone,
      email: agent.identity.email,
      ...(agent.identity.website && { url: agent.identity.website }),
      ...(agent.location.office_address && { address: agent.location.office_address }),
      ...(agent.location.service_areas && { areaServed: agent.location.service_areas }),
      ...(agent.identity.headshot_url && { image: agent.identity.headshot_url }),
    };

    return (
      <div style={cssVars as React.CSSProperties}>
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/<\/script>/gi, "<\\/script>") }}
        />
        <Analytics tracking={agent.integrations?.tracking} />
        <Template agent={agent} content={content} />
      </div>
    );
  } catch (err) {
    Sentry.captureException(err, { tags: { agentId: id } });
    notFound();
  }
}
