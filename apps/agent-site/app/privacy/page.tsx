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
    return { title: `Privacy Policy | ${agent.identity.name}` };
  } catch {
    return { title: "Privacy Policy" };
  }
}

function buildNjPrivacyContent(): string {
  return `### New Jersey Residents

Under the New Jersey Data Privacy Act (N.J.S.A. 56:8-166), you have the right to confirm, access, correct, delete, and obtain a portable copy of your personal data. You also have the right to opt out of the sale of personal data, targeted advertising, and profiling. We honor browser-based opt-out signals (Global Privacy Control). Contact us using the information below.`;
}

function buildGenericStatePrivacyContent(stateName: string): string {
  return `### ${stateName} Residents

${stateName} real estate laws and regulations apply. Please consult your state real estate commission and applicable privacy laws for specific requirements regarding your personal data rights. Contact us using the information below.`;
}

export default async function PrivacyPage({ searchParams }: PageProps) {
  const { agentId } = await searchParams;
  const id = resolveAgentId(agentId);

  let agent: ReturnType<typeof loadAgentConfig>;
  try {
    agent = loadAgentConfig(id);
  } catch (err) {
    Sentry.captureException(err, { tags: { agentId: id } });
    notFound();
  }

  const { above, below } = loadLegalContent(id, "privacy");
  const { identity, location } = agent;
  const stateName = getStateName(location.state);

  const statePrivacyContent = location.state === "NJ"
    ? buildNjPrivacyContent()
    : buildGenericStatePrivacyContent(stateName);

  const content = `# Privacy Policy

**Effective Date:** ${LEGAL_EFFECTIVE_DATE}

This privacy policy describes how ${identity.name}${identity.brokerage ? ` of ${identity.brokerage}` : ""} ("we", "us", "our") collects, uses, and protects your personal information when you use this website.

## Information We Collect

When you use our website, we may collect the following information:

- **Contact information** you provide through forms: name, email address, phone number, and property address
- **Property details** submitted for Comparative Market Analysis (CMA) requests
- **Usage data** including pages visited, time spent, and interactions with site features
- **Cookies and local storage** data used to remember your preferences${location.service_areas ? `

We serve the following areas: ${location.service_areas.join(", ")}.` : ""}

## How We Use Your Information

We use the information we collect to:

- Respond to your real estate inquiries and requests
- Prepare and deliver Comparative Market Analysis reports
- Communicate with you about properties and real estate services
- Improve our website and services
- Comply with legal obligations

## Cookies and Local Storage

This website uses cookies and browser local storage to:

- Remember your cookie consent preferences
- Improve site performance and user experience
- Track anonymous usage analytics

You can control cookies through your browser settings. Declining cookies may limit some site functionality.

## Information Sharing

We may share your information with:

- ${identity.brokerage ? `**${identity.brokerage}**` : "Our affiliated brokerage"} for real estate transaction purposes
- Service providers who assist in operating our website
- Legal authorities when required by law

**We do not sell your personal information to third parties.**

## Data Retention

We retain form submission data for two years for record-keeping purposes, after which it is securely deleted. Usage analytics data is retained in aggregate form indefinitely.

## TCPA Disclosure

Phone numbers collected through our forms are used solely to deliver requested real estate services. We do not share phone numbers with third parties for marketing purposes. You may opt out of communications at any time by contacting us or replying STOP to any text message.

## Third-Party Services

This website uses the following third-party services that may process your data:

- **Formspree** — form submission handling ([Formspree Privacy Policy](https://formspree.io/legal/privacy-policy/))
- **Cloudflare** — website hosting and performance ([Cloudflare Privacy Policy](https://www.cloudflare.com/privacypolicy/))
- **Sentry** — error tracking and monitoring ([Sentry Privacy Policy](https://sentry.io/privacy/))
- **Google Maps** — address autocomplete ([Google Privacy Policy](https://policies.google.com/privacy))

## Your Rights

${statePrivacyContent}

### California Residents (CCPA Notice)

If you are a California resident, you have the right to:

- Know what personal information we collect about you
- Request deletion of your personal information
- Opt out of the sale of your personal information (we do not sell personal data)
- Non-discrimination for exercising your privacy rights

## Contact Us

If you have questions about this privacy policy, contact us at:

**Email:** [${identity.email}](mailto:${identity.email})

*Last updated: ${LEGAL_EFFECTIVE_DATE}*`;

  return (
    <LegalPageLayout agent={agent} agentId={id} customAbove={above} customBelow={below}>
      <MarkdownContent content={content} />
    </LegalPageLayout>
  );
}
