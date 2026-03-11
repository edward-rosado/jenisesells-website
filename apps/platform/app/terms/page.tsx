import type { Metadata } from "next";
import Link from "next/link";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";

export const metadata: Metadata = {
  title: "Terms of Service | Real Estate Star",
  description:
    "Real Estate Star Terms of Service -- subscription, acceptable use, and liability.",
};

export default function TermsPage() {
  return (
    <LegalPageLayout>
      <h1 className="text-3xl font-bold text-white mb-2">Terms of Service</h1>
      <p className="text-sm text-gray-400 mb-8">
        Effective Date: March 11, 2026 | Last Updated: March 11, 2026
      </p>

      <Section title="1. Acceptance of Terms">
        <p>
          By accessing or using Real Estate Star (&quot;the Service,&quot;
          &quot;Platform,&quot; &quot;we,&quot; &quot;us,&quot; or
          &quot;our&quot;) at real-estate-star.com, you (&quot;Subscriber,&quot;
          &quot;Agent,&quot; &quot;you&quot;) agree to be bound by these Terms of
          Service (&quot;Terms&quot;). If you are entering into these Terms on
          behalf of a business or brokerage, you represent that you have
          authority to bind that entity.
        </p>
        <p>If you do not agree to these Terms, do not use the Service.</p>
      </Section>

      <Section title="2. Description of Service">
        <p>
          Real Estate Star is a software-as-a-service (SaaS) platform that
          provides licensed real estate professionals with tools including:
        </p>
        <ul>
          <li>
            <strong>Automated lead response</strong> -- AI-assisted email and
            messaging drafts
          </li>
          <li>
            <strong>Website deployment</strong> -- white-label agent websites
            hosted on your behalf
          </li>
          <li>
            <strong>CMA generation</strong> -- market data summaries for agent
            review
          </li>
          <li>
            <strong>Email management</strong> -- integration with third-party
            email providers
          </li>
        </ul>
      </Section>

      <Section title="3. Eligibility">
        <p>You must:</p>
        <ul>
          <li>
            Be a licensed real estate agent or broker in good standing in your
            state of practice
          </li>
          <li>Be at least 18 years old</li>
          <li>Have the legal authority to enter into binding contracts</li>
          <li>
            Provide accurate, current, and complete information during
            registration
          </li>
        </ul>
        <p>
          We reserve the right to verify licensure and suspend accounts that
          cannot demonstrate active, valid licensure.
        </p>
      </Section>

      <Section title="4. Accounts and Access">
        <p>
          You agree to provide accurate identity, contact, license, and
          brokerage information. You are responsible for maintaining the
          confidentiality of your credentials. Each agent in a brokerage
          requires their own subscription unless a brokerage-wide agreement is
          in place.
        </p>
        <p>
          Notify us immediately at{" "}
          <a
            href="mailto:support@real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            support@real-estate-star.com
          </a>{" "}
          of any unauthorized use of your account.
        </p>
      </Section>

      <Section title="5. Subscription and Payment">
        <ul>
          <li>
            Fees are listed on our pricing page and billed in advance. All fees
            are in USD.
          </li>
          <li>
            Subscriptions auto-renew unless cancelled at least 24 hours before
            renewal.
          </li>
          <li>
            We may change fees with 30 days&apos; notice. Continued use
            constitutes acceptance.
          </li>
          <li>
            Accounts with failed payments receive a 7-day grace period before
            suspension.
          </li>
        </ul>
      </Section>

      <Section title="6. Acceptable Use">
        <p>You agree NOT to:</p>
        <ul>
          <li>
            Use the Platform to engage in unlicensed real estate activity
          </li>
          <li>
            Violate any federal, state, or local real estate law, including
            RESPA, Fair Housing Act, and state licensing statutes
          </li>
          <li>
            Send unsolicited commercial email (spam) using our email tools
          </li>
          <li>
            Misrepresent your identity, license status, or brokerage
            affiliation
          </li>
          <li>Scrape, reverse-engineer, or copy any part of the Platform</li>
          <li>Circumvent or disable any security features</li>
        </ul>
        <p>
          We reserve the right to suspend or terminate accounts violating these
          terms without refund.
        </p>
      </Section>

      <Section title="7. CMA and Market Data">
        <p>CMA reports generated by the Platform:</p>
        <ul>
          <li>
            Are <strong>estimates only</strong> and do not constitute appraisals
          </li>
          <li>
            Are not licensed appraisals under USPAP or any state appraisal
            licensing statute
          </li>
          <li>
            Should not be represented to clients as formal appraisals
          </li>
          <li>
            Are based on data sources that may contain errors or omissions
          </li>
        </ul>
        <p>
          You are responsible for verifying all market data independently before
          presenting it to clients.
        </p>
      </Section>

      <Section title="8. Website Deployment">
        <p>
          Websites deployed through the Platform must comply with all applicable
          real estate advertising rules, including state broker supervision
          requirements and MLS display rules. You retain ownership of your
          content; by uploading content, you grant us a non-exclusive license to
          host and display it as part of the Service.
        </p>
      </Section>

      <Section title="9. Data and Privacy">
        <p>
          Your use of the Platform is governed by our{" "}
          <Link
            href="/privacy"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            Privacy Policy
          </Link>
          , incorporated here by reference. You own your agent profile data,
          client data, and any content you upload.
        </p>
      </Section>

      <Section title="10. Intellectual Property">
        <ul>
          <li>
            <strong>Ours:</strong> The Platform, its design, software, and
            documentation are owned by Real Estate Star.
          </li>
          <li>
            <strong>Yours:</strong> You retain all rights to your content,
            client data, and agent profile.
          </li>
          <li>
            <strong>Feedback:</strong> Any feedback or suggestions you provide
            may be used by us without compensation or attribution.
          </li>
        </ul>
      </Section>

      <Section title="11. Disclaimers">
        <p className="uppercase text-xs tracking-wide">
          THE SERVICE IS PROVIDED &quot;AS IS&quot; AND &quot;AS AVAILABLE&quot;
          WITHOUT WARRANTIES OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
          LIMITED TO WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
          PURPOSE, AND NON-INFRINGEMENT.
        </p>
      </Section>

      <Section title="12. Limitation of Liability">
        <p className="uppercase text-xs tracking-wide">
          TO THE MAXIMUM EXTENT PERMITTED BY LAW, REAL ESTATE STAR SHALL NOT BE
          LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR
          PUNITIVE DAMAGES. OUR TOTAL LIABILITY SHALL NOT EXCEED THE FEES YOU
          PAID IN THE 3 MONTHS PRECEDING THE CLAIM.
        </p>
      </Section>

      <Section title="13. Dispute Resolution">
        <p>
          Before filing any formal claim, you agree to contact us at{" "}
          <a
            href="mailto:legal@real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            legal@real-estate-star.com
          </a>{" "}
          and attempt to resolve the dispute informally for 30 days. If
          informal resolution fails, disputes shall be resolved by binding
          arbitration under AAA Commercial Arbitration Rules.
        </p>
        <p>
          <strong>Class Action Waiver:</strong> You waive any right to
          participate in a class action lawsuit or class-wide arbitration.
        </p>
      </Section>

      <Section title="14. Governing Law">
        <p>
          These Terms are governed by the laws of the State of New Jersey,
          without regard to conflict of law principles.
        </p>
      </Section>

      <Section title="15. Changes to Terms">
        <p>
          We may update these Terms at any time. We will notify you via email or
          in-app notice at least 14 days before material changes take effect.
          Continued use after the effective date constitutes acceptance.
        </p>
      </Section>

      <Section title="16. Contact">
        <p>
          <strong>Real Estate Star</strong>
          <br />
          Email:{" "}
          <a
            href="mailto:legal@real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            legal@real-estate-star.com
          </a>
          <br />
          Website:{" "}
          <a
            href="https://real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
            target="_blank"
            rel="noopener noreferrer"
          >
            real-estate-star.com
          </a>
        </p>
      </Section>
    </LegalPageLayout>
  );
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="mt-10">
      <h2 className="text-xl font-semibold text-white mb-3 border-b border-gray-700 pb-2">
        {title}
      </h2>
      <div className="text-gray-300 leading-relaxed space-y-4 [&_ul]:list-disc [&_ul]:list-inside [&_ul]:space-y-1 [&_strong]:text-white [&_strong]:font-semibold">
        {children}
      </div>
    </section>
  );
}
