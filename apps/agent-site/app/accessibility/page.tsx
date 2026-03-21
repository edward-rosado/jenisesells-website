import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { loadAccountConfig, loadLegalContent } from "@/lib/config";
import { loadNavConfig } from "@/lib/nav-config";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";
import { MarkdownContent } from "@/components/legal/MarkdownContent";
import { LEGAL_EFFECTIVE_DATE } from "@/components/legal/constants";

interface PageProps {
  searchParams: Promise<{ accountId?: string }>;
}

function resolveHandle(accountId?: string): string {
  return accountId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}

export async function generateMetadata({ searchParams }: PageProps): Promise<Metadata> {
  const { accountId } = await searchParams;
  const handle = resolveHandle(accountId);
  try {
    const account = loadAccountConfig(handle);
    const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
    return { title: `Accessibility | ${name}` };
  } catch {
    return { title: "Accessibility" };
  }
}

export default async function AccessibilityPage({ searchParams }: PageProps) {
  const { accountId } = await searchParams;
  const handle = resolveHandle(accountId);

  let account: ReturnType<typeof loadAccountConfig>;
  try {
    account = loadAccountConfig(handle);
  } catch (err) {
    console.error("[agent-site] Failed to load account:", handle, err);
    notFound();
  }

  const { above, below } = loadLegalContent(handle, "accessibility");
  const name = account.agent?.name ?? account.broker?.name ?? account.brokerage.name;
  const email = account.agent?.email ?? account.contact_info?.find((c) => c.type === "email")?.value ?? "";

  const content = `# Accessibility Statement

**Effective Date:** ${LEGAL_EFFECTIVE_DATE}

${name} is committed to ensuring digital accessibility for people of all abilities. We strive to conform to the Web Content Accessibility Guidelines (WCAG) 2.1 Level AA standards.

## Our Commitment

We are continually improving the user experience for everyone and applying relevant accessibility standards. Our efforts include:

- Semantic HTML structure for screen reader compatibility
- Keyboard navigation support throughout the site
- Sufficient color contrast ratios
- Alt text for meaningful images
- Skip navigation links for keyboard users
- Responsive design for various devices and screen sizes

## Known Limitations

While we strive for full accessibility compliance, some areas may have limitations:

- Third-party content or plugins may not fully meet accessibility standards
- Some older PDF documents may not be fully accessible
- Interactive map features may have limited screen reader support

We are actively working to address these limitations.

## Feedback

We welcome your feedback on the accessibility of this website. If you encounter any accessibility barriers, please contact us:

**Email:** [${email}](mailto:${email})

We will make reasonable efforts to address accessibility concerns promptly.

## Enforcement

If you are not satisfied with our response, you may contact the U.S. Department of Justice, Civil Rights Division, or file a complaint through the ADA website.

*Last updated: ${LEGAL_EFFECTIVE_DATE}*`;

  const { navigation, enabledSections } = loadNavConfig(handle);

  return (
    <LegalPageLayout agent={account} accountId={handle} customAbove={above} customBelow={below} navigation={navigation} enabledSections={enabledSections}>
      <MarkdownContent content={content} />
    </LegalPageLayout>
  );
}
