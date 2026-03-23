import Link from "next/link";
import { notFound } from "next/navigation";
import { loadAccountConfig, loadAccountContent } from "@/features/config/config";
import { loadNavConfig } from "@/features/config/nav-config";
import { buildCssVariableStyle } from "@/features/config/branding";
import { Nav } from "@/features/shared/Nav";
import { Footer } from "@/features/sections/shared";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";
import { safeTelHref } from "@/features/lead-capture/safe-contact";
import type { ThankYouData } from "@/features/config/types";

interface PageProps {
  searchParams: Promise<{ accountId?: string }>;
}

const DEFAULT_THANK_YOU: ThankYouData = {
  heading: "Thank You!",
  subheading: "Your Free Home Value Report Is Being Prepared Now!",
  body: "{firstName} will send your personalized Comparative Market Analysis to your email shortly. Keep an eye on your inbox!",
  disclaimer: "This home value report is a Comparative Market Analysis (CMA) and is not an appraisal. It should not be considered the equivalent of an appraisal.",
  cta_call: "Call {firstName}: {phone}",
  cta_back: "Back to {firstName}'s Site",
};

function interpolate(template: string, vars: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (_, key) => vars[key] ?? `{${key}}`);
}

export default async function ThankYouPage({ searchParams }: PageProps) {
  const { accountId } = await searchParams;
  const handle = accountId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";

  let account: ReturnType<typeof loadAccountConfig>;
  try {
    account = loadAccountConfig(handle);
  } catch (err) {
    console.error("[agent-site] Failed to load account:", handle, err);
    notFound();
  }

  const content = loadAccountContent(handle, account);
  const thankYou = content.pages?.thank_you ?? DEFAULT_THANK_YOU;
  const navConfig = loadNavConfig(handle);
  const cssVars = buildCssVariableStyle(account.branding);
  const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
  const phone = account.agent?.phone ?? account.contact_info?.find((c) => c.type === "phone")?.value ?? "";
  const firstName = name.split(" ")[0];
  const vars = { firstName, phone };

  return (
    <div style={cssVars as React.CSSProperties}>
      <Nav account={account} navigation={navConfig.navigation} enabledSections={navConfig.enabledSections} />
      <main id="main-content" tabIndex={-1} className="pt-[74px] min-h-[70vh] flex items-center justify-center">
        <div className="text-center max-w-lg px-6">
          <div style={{
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
          }}>&#10003;</div>
          <h1 className="text-3xl font-bold mb-3" style={{ color: "var(--color-primary)" }}>
            {thankYou.heading}
          </h1>
          <p className="text-lg font-semibold mb-4" style={{ color: "var(--color-accent)" }}>
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
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "12px" }}>
            <a
              href={safeTelHref(phone)}
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
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/></svg>
              {interpolate(thankYou.cta_call ?? "Call {firstName}: {phone}", vars)}
            </a>
            <Link
              href="/"
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
      <Footer agent={account} accountId={handle} />
      <CookieConsentBanner accountId={handle} />
    </div>
  );
}
