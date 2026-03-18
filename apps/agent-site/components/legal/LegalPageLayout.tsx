import type { AccountConfig } from "@/lib/types";
import { buildCssVariableStyle } from "@/lib/branding";
import { Nav } from "@/components/Nav";
import { Footer } from "@/components/sections/shared/Footer";
import { CookieConsentBanner } from "./CookieConsentBanner";
import { MarkdownContent } from "./MarkdownContent";

interface LegalPageLayoutProps {
  agent: AccountConfig;
  accountId: string;
  children: React.ReactNode;
  customAbove?: string;
  customBelow?: string;
}

export function LegalPageLayout({
  agent, accountId, children, customAbove, customBelow,
}: LegalPageLayoutProps) {
  const cssVars = buildCssVariableStyle(agent.branding);
  return (
    <div style={cssVars as React.CSSProperties}>
      <Nav account={agent} />
      <main className="pt-[74px] min-h-[70vh] px-6 py-12" style={{ background: "#f5f5f5" }}>
        <div className="mx-auto max-w-3xl" style={{ background: "#fff", borderRadius: 12, padding: "clamp(24px, 5vw, 48px)", boxShadow: "0 2px 12px rgba(0,0,0,0.06)" }}>
          {customAbove && <div className="mb-8"><MarkdownContent content={customAbove} /></div>}
          {children}
          {customBelow && <div className="mt-8"><MarkdownContent content={customBelow} /></div>}
        </div>
      </main>
      <Footer agent={agent} accountId={accountId} />
      <CookieConsentBanner accountId={accountId} />
    </div>
  );
}
