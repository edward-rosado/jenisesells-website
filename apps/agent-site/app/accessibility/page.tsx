import * as Sentry from "@sentry/nextjs";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { loadAgentConfig, loadLegalContent } from "@/lib/config";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";
import { MarkdownContent } from "@/components/legal/MarkdownContent";
import { LEGAL_EFFECTIVE_DATE } from "@/components/legal/constants";

interface PageProps {
  searchParams: Promise<{ agentId?: string }>;
}

function resolveAgentId(agentId?: string): string {
  return agentId || process.env.DEFAULT_AGENT_ID || "jenise-buckalew";
}

export async function generateMetadata({ searchParams }: PageProps): Promise<Metadata> {
  const { agentId } = await searchParams;
  const id = resolveAgentId(agentId);
  try {
    const agent = await loadAgentConfig(id);
    return { title: `Accessibility | ${agent.identity.name}` };
  } catch {
    return { title: "Accessibility" };
  }
}

export default async function AccessibilityPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;
  const id = resolveAgentId(agentId);

  let agent: Awaited<ReturnType<typeof loadAgentConfig>>;
  try {
    agent = await loadAgentConfig(id);
  } catch (err) {
    Sentry.captureException(err, { tags: { agentId: id } });
    notFound();
  }

  const { above, below } = await loadLegalContent(id, "accessibility");
  const { identity } = agent;

  const content = `# Accessibility Statement

**Effective Date:** ${LEGAL_EFFECTIVE_DATE}

${identity.name} is committed to ensuring digital accessibility for people of all abilities. We strive to conform to the Web Content Accessibility Guidelines (WCAG) 2.1 Level AA standards.

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

**Email:** [${identity.email}](mailto:${identity.email})

We will make reasonable efforts to address accessibility concerns promptly.

## Enforcement

If you are not satisfied with our response, you may contact the U.S. Department of Justice, Civil Rights Division, or file a complaint through the ADA website.

*Last updated: ${LEGAL_EFFECTIVE_DATE}*`;

  return (
    <LegalPageLayout agent={agent} agentId={id} customAbove={above} customBelow={below}>
      <MarkdownContent content={content} />
    </LegalPageLayout>
  );
}
