import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { loadAccountConfig, loadLegalContent } from "@/features/config/config";
import { loadNavConfig } from "@/features/config/nav-config";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";
import { MarkdownContent } from "@/components/legal/MarkdownContent";
import { LEGAL_EFFECTIVE_DATE, getStateName } from "@/components/legal/constants";
import { safeMailtoHref } from "@/lib/safe-contact";

interface PageProps {
  searchParams: Promise<{ accountId?: string }>;
}

const SAFE_HANDLE = /^[a-z0-9-]+$/;

function resolveHandle(accountId?: string): string {
  const raw = accountId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
  return SAFE_HANDLE.test(raw) ? raw : (process.env.DEFAULT_AGENT_ID || "jenise-buckalew");
}

export async function generateMetadata({ searchParams }: PageProps): Promise<Metadata> {
  const { accountId } = await searchParams;
  const handle = resolveHandle(accountId);
  try {
    const account = loadAccountConfig(handle);
    const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
    return { title: `Terms of Use | ${name}` };
  } catch {
    return { title: "Terms of Use" };
  }
}

function buildNjTermsContent(identity: { name: string; brokerage?: string; brokerage_id?: string; license_id?: string }): string {
  return `## New Jersey Fair Housing

In accordance with the New Jersey Fair Housing Act (NJSA 10:5-12), all real estate services are provided without discrimination. If you believe you have experienced housing discrimination, you may file a complaint with the New Jersey Division on Civil Rights at (609) 292-4605.

## NJ Real Estate Commission

${identity.name} is licensed by the New Jersey Real Estate Commission.${identity.brokerage ? ` Brokerage: ${identity.brokerage}` : ""}${identity.brokerage_id ? ` (License #${identity.brokerage_id})` : ""}.${identity.license_id ? ` Agent License #${identity.license_id}.` : ""}`;
}

function buildGenericStateContent(stateName: string): string {
  return `## State-Specific Notices (${stateName})

${stateName} real estate laws and regulations apply. Please consult your state real estate commission for specific requirements.`;
}

export default async function TermsPage({ searchParams }: PageProps) {
  const { accountId } = await searchParams;
  const handle = resolveHandle(accountId);

  let account: ReturnType<typeof loadAccountConfig>;
  try {
    account = loadAccountConfig(handle);
  } catch (err) {
    console.error("[agent-site] Failed to load account:", handle, err);
    notFound();
  }

  const { above, below } = loadLegalContent(handle, "terms");
  const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
  const email = account.agent?.email ?? account.contact_info?.find((c) => c.type === "email")?.value ?? "";
  const brokerageName = account.brokerage.name;
  const licenseNumber = account.agent?.license_number;
  const brokerageLicenseNumber = account.brokerage.license_number;
  const { location } = account;
  const stateName = getStateName(location.state);

  const identity = {
    name,
    brokerage: brokerageName || undefined,
    brokerage_id: brokerageLicenseNumber || undefined,
    license_id: licenseNumber || undefined,
  };

  const stateContent = location.state === "NJ"
    ? buildNjTermsContent(identity)
    : buildGenericStateContent(stateName);

  const content = `# Terms of Use

**Effective Date:** ${LEGAL_EFFECTIVE_DATE}

By accessing and using this website operated by ${name}${brokerageName ? ` of ${brokerageName}` : ""}, you agree to the following terms and conditions.${licenseNumber ? ` Licensed as #${licenseNumber}.` : ""}

## Real Estate Services

${name} is a licensed real estate professional in the state of ${stateName}. All real estate services are subject to applicable state and federal regulations.

## CMA Disclaimer

The Comparative Market Analysis (CMA) reports provided through this website are estimates based on publicly available data and recent comparable sales. **CMA reports are not appraisals** and should not be used as such. Property values are estimates only and may differ from actual market value. For an official property valuation, please consult a licensed appraiser.

## Fair Housing Commitment

We are committed to the principles of the Federal Fair Housing Act (42 U.S.C. 3601 et seq.)${location.state === "NJ" ? " and the New Jersey Law Against Discrimination (NJSA 10:5-1 et seq.)" : ""}. We do not discriminate based on race, creed, color, national origin, nationality, ancestry, age, sex (including pregnancy), gender identity or expression, disability, marital status, domestic partnership or civil union status, affectional or sexual orientation, familial status, source of lawful income or rent payments, military service, or any other protected class under federal or state law. All properties are available on an equal opportunity basis.

${stateContent}

## Intellectual Property

All content on this website, including text, images, logos, and design, is the property of ${name} or used with permission. You may not reproduce, distribute, or create derivative works without prior written consent.

## Limitation of Liability

${name} makes no warranties about the accuracy or completeness of information on this website. We are not liable for any damages arising from your use of this site or reliance on its content, including but not limited to CMA estimates, property information, or market data.

## Third-Party Links

This website may contain links to third-party websites. We are not responsible for the content or privacy practices of external sites.

## Governing Law

These terms are governed by the laws of the state of ${stateName}. Any disputes shall be resolved in the courts of ${stateName}.

## Contact Us

For questions about these terms, contact us at:

**Email:** [${email}](${safeMailtoHref(email)})

*Last updated: ${LEGAL_EFFECTIVE_DATE}*`;

  const { navigation, enabledSections } = loadNavConfig(handle);

  return (
    <LegalPageLayout agent={account} accountId={handle} customAbove={above} customBelow={below} navigation={navigation} enabledSections={enabledSections}>
      <MarkdownContent content={content} />
    </LegalPageLayout>
  );
}
