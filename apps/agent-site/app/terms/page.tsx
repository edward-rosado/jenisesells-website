import * as Sentry from "@sentry/nextjs";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { loadAgentConfig, loadLegalContent } from "@/lib/config";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";
import { MarkdownContent } from "@/components/legal/MarkdownContent";
import { LEGAL_EFFECTIVE_DATE, getStateName } from "@/components/legal/constants";

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
    const agent = loadAgentConfig(id);
    return { title: `Terms of Use | ${agent.identity.name}` };
  } catch {
    return { title: "Terms of Use" };
  }
}

export default async function TermsPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;
  const id = resolveAgentId(agentId);

  let agent: ReturnType<typeof loadAgentConfig>;
  try {
    agent = loadAgentConfig(id);
  } catch (err) {
    Sentry.captureException(err, { tags: { agentId: id } });
    notFound();
  }

  const { above, below } = loadLegalContent(id, "terms");
  const { identity, location } = agent;
  const stateName = getStateName(location.state);

  const content = `# Terms of Use

**Effective Date:** ${LEGAL_EFFECTIVE_DATE}

By accessing and using this website operated by ${identity.name}${identity.brokerage ? ` of ${identity.brokerage}` : ""}, you agree to the following terms and conditions.${identity.license_id ? ` Licensed as #${identity.license_id}.` : ""}

## Real Estate Services

${identity.name} is a licensed real estate professional in the state of ${stateName}. All real estate services are subject to applicable state and federal regulations.

## CMA Disclaimer

The Comparative Market Analysis (CMA) reports provided through this website are estimates based on publicly available data and recent comparable sales. **CMA reports are not appraisals** and should not be used as such. Property values are estimates only and may differ from actual market value. For an official property valuation, please consult a licensed appraiser.

## Fair Housing Commitment

We are committed to the principles of the Fair Housing Act. We do not discriminate based on race, color, religion, sex, national origin, familial status, disability, or any other protected class. All properties are available on an equal opportunity basis.

## Intellectual Property

All content on this website, including text, images, logos, and design, is the property of ${identity.name} or used with permission. You may not reproduce, distribute, or create derivative works without prior written consent.

## Limitation of Liability

${identity.name} makes no warranties about the accuracy or completeness of information on this website. We are not liable for any damages arising from your use of this site or reliance on its content, including but not limited to CMA estimates, property information, or market data.

## Third-Party Links

This website may contain links to third-party websites. We are not responsible for the content or privacy practices of external sites.

## Governing Law

These terms are governed by the laws of the state of ${stateName}. Any disputes shall be resolved in the courts of ${stateName}.

## Contact Us

For questions about these terms, contact us at:

**Email:** [${identity.email}](mailto:${identity.email})

*Last updated: ${LEGAL_EFFECTIVE_DATE}*`;

  return (
    <LegalPageLayout agent={agent} agentId={id} customAbove={above} customBelow={below}>
      <MarkdownContent content={content} />
    </LegalPageLayout>
  );
}
