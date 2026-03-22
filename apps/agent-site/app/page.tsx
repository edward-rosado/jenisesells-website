import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { loadAccountConfig, loadAccountContent } from "@/features/config/config";
import { buildCssVariableStyle } from "@/features/config/branding";
import { getTemplate } from "@/features/templates";
import { Analytics } from "@/features/shared/Analytics";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";

interface PageProps {
  searchParams: Promise<{ accountId?: string; template?: string }>;
}

export const revalidate = 60; // ISR: revalidate every 60 seconds

function resolveHandle(accountId?: string): string {
  // In production, always use the bound account — never trust query params (tenant confusion)
  // Preview deploys set PREVIEW=true so ?accountId works for QA
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) {
    return process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
  }
  return accountId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}

function resolveTemplateOverride(template?: string): string | undefined {
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) return undefined;
  return template;
}

export async function generateMetadata({ searchParams }: PageProps): Promise<Metadata> {
  const { accountId } = await searchParams;
  const handle = resolveHandle(accountId);
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
  const { accountId, template: templateOverride } = await searchParams;
  const handle = resolveHandle(accountId);

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
        <CookieConsentBanner accountId={handle} />
      </div>
    );
  } catch (err) {
    console.error("[agent-site] Failed to load account:", handle, err);
    notFound();
  }
}
