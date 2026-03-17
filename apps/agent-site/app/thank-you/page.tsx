import * as Sentry from "@sentry/nextjs";
import { notFound } from "next/navigation";
import { loadAgentConfig } from "@/lib/config";
import { buildCssVariableStyle } from "@/lib/branding";
import { Nav } from "@/components/Nav";
import { Footer } from "@/components/sections";
import { CookieConsentBanner } from "@/components/legal/CookieConsentBanner";

interface PageProps {
  searchParams: Promise<{ agentId?: string; email?: string }>;
}

export default async function ThankYouPage({ searchParams }: PageProps) {
  const { agentId, email } = await searchParams;
  const id = agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";

  // Load data in try/catch — return JSX outside so React can catch render errors.
  let agent: ReturnType<typeof loadAgentConfig>;
  try {
    agent = loadAgentConfig(id);
  } catch (err) {
    Sentry.captureException(err, { tags: { agentId: id } });
    notFound(); // typed as never — execution stops here on failure
  }

  const cssVars = buildCssVariableStyle(agent.branding);
  const firstName = agent.identity.name.split(" ")[0];

  return (
    <div style={cssVars as React.CSSProperties}>
      <Nav agent={agent} />
      <main className="pt-[74px] min-h-[70vh] flex items-center justify-center">
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
            Thank You!
          </h1>
          <p className="text-lg font-semibold mb-4" style={{ color: "var(--color-accent)" }}>
            Your Free Home Value Report Is Being Prepared Now!
          </p>
          {email && (
            <p className="text-gray-700 mb-4 text-base">
              We&apos;ll send your personalized report to{" "}
              <strong>{email}</strong>. Keep an eye on your inbox!
            </p>
          )}
          {!email && (
            <p className="text-gray-600 mb-4">
              {firstName} will send your personalized Comparative Market Analysis
              to your email shortly. Keep an eye on your inbox!
            </p>
          )}
          <p className="text-gray-500 text-sm italic mb-8">
            This home value report is a Comparative Market Analysis (CMA) and is not an appraisal.
            It should not be considered the equivalent of an appraisal.
          </p>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "12px" }}>
            <a
              href={`tel:${agent.identity.phone.replace(/\D/g, "")}`}
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
              Call {firstName}: {agent.identity.phone}
            </a>
            <a
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
              Back to {firstName}&apos;s Site
            </a>
          </div>
        </div>
      </main>
      <Footer agent={agent} agentId={id} />
      <CookieConsentBanner agentId={id} />
    </div>
  );
}
