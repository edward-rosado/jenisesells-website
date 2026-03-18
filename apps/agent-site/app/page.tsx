import type { Metadata } from "next";
import * as Sentry from "@sentry/nextjs";
import { notFound } from "next/navigation";
import { loadAccountConfig, loadAccountContent } from "@/lib/config";
import { buildCssVariableStyle } from "@/lib/branding";
import { getTemplate } from "@/templates";
import { Analytics } from "@/components/Analytics";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";

interface PageProps {
  searchParams: Promise<{ agentId?: string; template?: string }>;
}

export const revalidate = 60; // ISR: revalidate every 60 seconds

function resolveHandle(agentId?: string): string {
  // In production, always use the bound account — never trust query params (tenant confusion)
  // Preview deploys set PREVIEW=true so ?agentId works for QA
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) {
    return process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
  }
  return agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}

function resolveTemplateOverride(template?: string): string | undefined {
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) return undefined;
  return template;
}

export async function generateMetadata({ searchParams }: PageProps): Promise<Metadata> {
  const { agentId } = await searchParams;
  const handle = resolveHandle(agentId);
  try {
    const account = loadAccountConfig(handle);
    const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
    const title = account.agent?.title ?? "Real Estate";
    return {
      title: `${name} | ${title}`,
      description: account.agent?.tagline ?? `${name} — serving ${account.location.service_areas?.join(", ") ?? account.location.state}`,
      openGraph: {
        title: name,
        description: account.agent?.tagline ?? "",
        type: "website",
      },
    };
  } catch {
    return { title: "Real Estate Agent" };
  }
}

export default async function AgentPage({ searchParams }: PageProps) {
  const { agentId, template: templateOverride } = await searchParams;
  const handle = resolveHandle(agentId);

  try {
    const account = loadAccountConfig(handle);
    const content = loadAccountContent(handle, account);

    const cssVars = buildCssVariableStyle(account.branding);
    const Template = getTemplate(resolveTemplateOverride(templateOverride) ?? account.template);

    const agentName = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
    const jsonLd = {
      "@context": "https://schema.org",
      "@type": "RealEstateAgent",
      name: agentName,
      ...(account.agent?.phone && { telephone: account.agent.phone }),
      ...(account.agent?.email && { email: account.agent.email }),
      ...(account.integrations?.hosting && { url: `https://${account.integrations.hosting}` }),
      ...(account.brokerage.office_address && { address: account.brokerage.office_address }),
      ...(account.location.service_areas && { areaServed: account.location.service_areas }),
      ...(account.agent?.headshot_url && { image: account.agent.headshot_url }),
    };

    // JSON-LD uses JSON.stringify on controlled data — safe, not user HTML
    const jsonLdHtml = JSON.stringify(jsonLd).replace(/<\/script>/gi, "<\\/script>");

    return (
      <div style={cssVars as React.CSSProperties}>
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: jsonLdHtml }}
        />
        <Analytics tracking={account.integrations?.tracking} />
        <Template account={account} content={content} />
        <CookieConsentBanner agentId={handle} />
      </div>
    );
  } catch (err) {
    Sentry.captureException(err, { tags: { agentId: handle } });
    notFound();
  }
}
