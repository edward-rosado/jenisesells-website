import { createElement } from "react";
import type { Metadata } from "next";
import * as Sentry from "@sentry/nextjs";
import { notFound } from "next/navigation";
import { loadAccountConfig, loadAccountContent, loadAgentConfig, loadAgentContent } from "@/lib/config";
import { buildCssVariableStyle } from "@/lib/branding";
import { getTemplate } from "@/templates";
import { Analytics } from "@/components/Analytics";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";

interface PageProps {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ agentId?: string }>;
}

export const revalidate = 60; // ISR: revalidate every 60 seconds

function resolveHandle(agentId?: string): string {
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) {
    return process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
  }
  return agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}

export async function generateMetadata({ params, searchParams }: PageProps): Promise<Metadata> {
  const { id } = await params;
  const { agentId } = await searchParams;
  const handle = resolveHandle(agentId);
  try {
    const account = loadAccountConfig(handle);
    const agentConfig = loadAgentConfig(handle, id);
    const name = agentConfig.name;
    const title = agentConfig.title ?? "Real Estate";
    return {
      title: `${name} | ${title}`,
      description:
        agentConfig.tagline ??
        `${name} — serving ${account.location.service_areas?.join(", ") ?? account.location.state}`,
      openGraph: {
        title: name,
        description: agentConfig.tagline ?? "",
        type: "website",
        ...(agentConfig.headshot_url && { images: [agentConfig.headshot_url] }),
      },
    };
  } catch {
    return { title: "Real Estate Agent" };
  }
}

export default async function AgentSubPage({ params, searchParams }: PageProps) {
  const { id } = await params;
  const { agentId } = await searchParams;
  const handle = resolveHandle(agentId);

  let account: ReturnType<typeof loadAccountConfig>;
  try {
    account = loadAccountConfig(handle);
  } catch (err) {
    Sentry.captureException(err, { tags: { handle } });
    notFound();
  }

  let agentConfig: ReturnType<typeof loadAgentConfig>;
  try {
    agentConfig = loadAgentConfig(handle, id);
  } catch (err) {
    Sentry.captureException(err, { tags: { handle, agentId: id } });
    notFound();
  }

  const agentContent =
    loadAgentContent(handle, id) ?? loadAccountContent(handle, account);

  const cssVars = buildCssVariableStyle(account.branding);
  const TemplateComponent = getTemplate(account.template);

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "RealEstateAgent",
    name: agentConfig.name,
    ...(agentConfig.phone && { telephone: agentConfig.phone }),
    ...(agentConfig.email && { email: agentConfig.email }),
    ...(account.integrations?.hosting && {
      url: `https://${account.integrations.hosting}`,
    }),
    ...(account.brokerage.office_address && {
      address: account.brokerage.office_address,
    }),
    ...(account.location.service_areas && {
      areaServed: account.location.service_areas,
    }),
    ...(agentConfig.headshot_url && { image: agentConfig.headshot_url }),
    worksFor: {
      "@type": "RealEstateAgent",
      name: account.brokerage.name,
    },
  };

  // JSON-LD uses JSON.stringify on controlled data — safe, not user HTML
  const jsonLdString = JSON.stringify(jsonLd).replace(/<\/script>/gi, "<\/script>");

  return (
    <div style={cssVars as React.CSSProperties}>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: jsonLdString }}
      />
      <Analytics tracking={account.integrations?.tracking} />
      {createElement(TemplateComponent, { account, content: agentContent, agent: agentConfig })}
      <CookieConsentBanner agentId={handle} />
    </div>
  );
}
