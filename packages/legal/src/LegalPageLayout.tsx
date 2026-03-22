import type { AccountBranding } from "./branding";
import { buildCssVariableStyle } from "./branding";
import { CookieConsentBanner } from "./CookieConsentBanner";
import { MarkdownContent } from "./MarkdownContent";

interface LegalPageLayoutProps {
  /** Agent branding used to inject CSS custom properties. */
  branding: AccountBranding;
  accountId: string;
  children: React.ReactNode;
  customAbove?: string;
  customBelow?: string;
  /** Rendered as the site navigation. Pass <Nav /> from the consuming app. */
  nav?: React.ReactNode;
  /** Rendered as the site footer. Pass <Footer /> from the consuming app. */
  footer?: React.ReactNode;
}

export function LegalPageLayout({
  branding,
  accountId,
  children,
  customAbove,
  customBelow,
  nav,
  footer,
}: LegalPageLayoutProps) {
  const cssVars = buildCssVariableStyle(branding);
  return (
    <div style={cssVars as React.CSSProperties}>
      {nav}
      <main className="pt-[74px] min-h-[70vh] px-6 py-12" style={{ background: "#f5f5f5" }}>
        <div className="mx-auto max-w-3xl" style={{ background: "#fff", borderRadius: 12, padding: "clamp(24px, 5vw, 48px)", boxShadow: "0 2px 12px rgba(0,0,0,0.06)" }}>
          {customAbove && <div className="mb-8"><MarkdownContent content={customAbove} /></div>}
          {children}
          {customBelow && <div className="mt-8"><MarkdownContent content={customBelow} /></div>}
        </div>
      </main>
      {footer}
      <CookieConsentBanner accountId={accountId} />
    </div>
  );
}
