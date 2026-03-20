import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { loadAccountConfig, loadAccountContent, loadAgentConfig, loadAgentContent } from "@/lib/config";
import { loadNavConfig } from "@/lib/nav-config";
import { buildCssVariableStyle } from "@/lib/branding";
import { Nav } from "@/components/Nav";
import { Footer } from "@/components/sections";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";
import type { AccountConfig, AgentConfig, ThankYouData } from "@/lib/types";

interface PageProps {
  params: Promise<{ id: string }>;
}

export const revalidate = 60; // ISR: revalidate every 60 seconds

function resolveHandle(): string {
  if (process.env.NODE_ENV === "production" && !process.env.PREVIEW) {
    return process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
  }
  return process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}

const DEFAULT_THANK_YOU: ThankYouData = {
  heading: "Thank You!",
  subheading: "Your Free Home Value Report Is Being Prepared Now!",
  body: "{firstName} will send your personalized Comparative Market Analysis to your email shortly. Keep an eye on your inbox!",
  disclaimer:
    "This home value report is a Comparative Market Analysis (CMA) and is not an appraisal. It should not be considered the equivalent of an appraisal.",
  cta_call: "Call {firstName}: {phone}",
  cta_back: "Back to {firstName}'s Site",
};

function interpolate(template: string, vars: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (_, key) => vars[key] ?? `{${key}}`);
}

/** Resolve agent display identity from AgentConfig or AccountConfig fallback */
function resolveIdentity(
  agentConfig: AgentConfig | undefined,
  account: AccountConfig,
): { id: string; name: string; phone: string } {
  if (agentConfig) {
    return { id: agentConfig.id, name: agentConfig.name, phone: agentConfig.phone };
  }
  const fallback = account.agent;
  return {
    id: fallback?.id ?? account.handle,
    name: fallback?.name ?? account.brokerage.name,
    phone: fallback?.phone ?? account.brokerage.office_phone ?? "",
  };
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { id } = await params;
  const handle = resolveHandle();
  try {
    loadAccountConfig(handle); // validate account exists
    const agentConfig = loadAgentConfig(handle, id);
    return {
      title: `Thank You | ${agentConfig.name}`,
      description: `Thank you for reaching out to ${agentConfig.name}.`,
      robots: { index: false },
    };
  } catch {
    try {
      const account = loadAccountConfig(handle);
      const name = account.agent?.name ?? account.brokerage.name;
      return {
        title: `Thank You | ${name}`,
        description: `Thank you for reaching out to ${name}.`,
        robots: { index: false },
      };
    } catch {
      return { title: "Thank You" };
    }
  }
}

export default async function AgentThankYouPage({ params }: PageProps) {
  const { id } = await params;
  const handle = resolveHandle();

  let account: AccountConfig;
  try {
    account = loadAccountConfig(handle);
  } catch (err) {
    console.error("[agent-site] Failed to load account:", handle, err);
    notFound();
  }

  let agentConfig: AgentConfig | undefined;
  try {
    agentConfig = loadAgentConfig(handle, id);
  } catch (err) {
    console.error("[agent-site] Failed to load agent:", handle, id, err);
    notFound();
  }

  const content =
    loadAgentContent(handle, id) ?? loadAccountContent(handle, account);

  const thankYou = content.pages?.thank_you ?? DEFAULT_THANK_YOU;
  const navConfig = loadNavConfig(handle);
  const cssVars = buildCssVariableStyle(account.branding);
  const identity = resolveIdentity(agentConfig, account);
  const firstName = identity.name.split(" ")[0];
  const vars = { firstName, phone: identity.phone };

  return (
    <div style={cssVars as React.CSSProperties}>
      <Nav account={account} navigation={navConfig.navigation} enabledSections={navConfig.enabledSections} />
      <main className="pt-[74px] min-h-[70vh] flex items-center justify-center">
        <div className="text-center max-w-lg px-6">
          <div
            style={{
              width: "80px",
              height: "80px",
              borderRadius: "50%",
              backgroundColor: "var(--color-primary)",
              color: "#fff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: "40px",
              margin: "0 auto 24px",
            }}
          >
            &#10003;
          </div>
          <h1
            className="text-3xl font-bold mb-3"
            style={{ color: "var(--color-primary)" }}
          >
            {thankYou.heading}
          </h1>
          <p
            className="text-lg font-semibold mb-4"
            style={{ color: "var(--color-accent)" }}
          >
            {interpolate(thankYou.subheading, vars)}
          </p>
          {thankYou.body && (
            <p className="text-gray-600 mb-4">
              {interpolate(thankYou.body, vars)}
            </p>
          )}
          {thankYou.disclaimer && (
            <p className="text-gray-500 text-sm italic mb-8">
              {interpolate(thankYou.disclaimer, vars)}
            </p>
          )}
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: "12px",
            }}
          >
            {identity.phone && (
              <a
                href={`tel:${identity.phone.replace(/\D/g, "")}`}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "8px",
                  padding: "12px 28px",
                  borderRadius: "30px",
                  fontWeight: 700,
                  color: "#fff",
                  backgroundColor: "var(--color-primary)",
                  textDecoration: "none",
                  transition: "transform 200ms ease",
                }}
              >
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z" />
                </svg>
                {interpolate(thankYou.cta_call ?? "Call {firstName}: {phone}", vars)}
              </a>
            )}
            <Link
              href={`/agents/${id}`}
              style={{
                display: "inline-flex",
                alignItems: "center",
                padding: "12px 28px",
                borderRadius: "30px",
                fontWeight: 700,
                color: "var(--color-primary)",
                backgroundColor: "var(--color-accent)",
                textDecoration: "none",
                transition: "transform 200ms ease",
              }}
            >
              {interpolate(thankYou.cta_back ?? "Back to {firstName}'s Site", vars)}
            </Link>
          </div>
        </div>
      </main>
      <Footer agent={account} accountId={identity.id} />
      <CookieConsentBanner accountId={handle} />
    </div>
  );
}
