import type { Metadata } from "next";
import Link from "next/link";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";

export const metadata: Metadata = {
  title: "Privacy Policy | Real Estate Star",
  description:
    "Real Estate Star Privacy Policy -- data collection, CCPA compliance, and your rights.",
};

export default function PrivacyPage() {
  return (
    <LegalPageLayout>
      <h1 className="text-3xl font-bold text-white mb-2">Privacy Policy</h1>
      <p className="text-sm text-gray-400 mb-8">
        Effective Date: March 11, 2026 | Last Updated: March 16, 2026
      </p>

      <Section title="1. Overview">
        <p>
          Real Estate Star (&quot;we,&quot; &quot;us,&quot; &quot;our&quot;)
          operates real-estate-star.com. This Privacy Policy explains what data
          we collect, how we use it, and your rights -- including rights under
          the California Consumer Privacy Act (CCPA).
        </p>
        <p>
          By using the Platform, you agree to the practices described here.
        </p>
      </Section>

      <Section title="2. Data We Collect">
        <h3 className="text-lg font-medium text-gray-200 mt-6 mb-2">
          2.1 Data You Provide Directly
        </h3>
        <ul>
          <li>
            <strong>Account information:</strong> name, email address, phone
            number, real estate license number, brokerage name
          </li>
          <li>
            <strong>Profile URL:</strong> Zillow or Realtor.com profile URL you
            submit during onboarding
          </li>
          <li>
            <strong>Branding preferences:</strong> colors, fonts, and other
            customization choices
          </li>
          <li>
            <strong>Content:</strong> photos, bios, and other materials you
            upload to your agent website
          </li>
        </ul>

        <h3 className="text-lg font-medium text-gray-200 mt-6 mb-2">
          2.2 Data We Collect Automatically
        </h3>
        <ul>
          <li>
            <strong>Session data:</strong> onboarding session state, chat
            history, and tool outputs during your onboarding flow
          </li>
          <li>
            <strong>Usage data:</strong> pages visited, features used, and
            general interaction patterns
          </li>
          <li>
            <strong>Payment data:</strong> Stripe handles all payment
            processing. We receive only a transaction confirmation and your
            email -- we never see or store full card numbers
          </li>
        </ul>

        <h3 className="text-lg font-medium text-gray-200 mt-6 mb-2">
          2.3 Data from Third-Party Sources
        </h3>
        <ul>
          <li>
            <strong>Public profile data:</strong> We may retrieve publicly
            available information from your Zillow or Realtor.com profile URL to
            pre-fill your agent profile during onboarding
          </li>
          <li>
            <strong>Google account data:</strong> If you connect your Google
            account, we receive an OAuth access token and your Google account
            email address. See Section 6 for full details.
          </li>
        </ul>
      </Section>

      <Section title="3. How We Use Your Data">
        <p>
          We use your data to deliver the Platform (website deployment, CMA
          generation, lead management), personalize your experience, process
          your subscription and trial, send transactional emails, improve and
          develop Platform features using anonymized usage data, and comply with
          legal obligations.
        </p>
        <p>
          We do <strong>not</strong> sell your personal data to third parties.
        </p>
      </Section>

      <Section title="4. SMS and Phone Communications (TCPA)">
        <p>
          If you or your leads opt in to SMS/text message features, the
          recipient explicitly consents to receive automated text messages to
          the phone number provided. By opting in, the recipient acknowledges:
        </p>
        <ul>
          <li>They are the account owner or authorized user of the phone number</li>
          <li>They consent to receive recurring automated messages</li>
          <li>Message and data rates may apply</li>
          <li>To opt out, reply STOP to any message</li>
          <li>Consent is not a condition of purchasing any property or service</li>
        </ul>
        <p>
          We comply with the Telephone Consumer Protection Act (47 U.S.C.
          &sect; 227) and maintain records of all opt-in consents.
        </p>
      </Section>

      <Section title="5. Email Communications (CAN-SPAM)">
        <p>
          All marketing emails sent through Real Estate Star comply with the
          CAN-SPAM Act (15 U.S.C. &sect; 7701):
        </p>
        <ul>
          <li>All emails include the agent&apos;s business physical address</li>
          <li>All emails allow recipients to unsubscribe via a clear link</li>
          <li>We process unsubscribe requests within 3 business days</li>
          <li>We do not use deceptive subject lines or headers</li>
          <li>
            AI-generated email drafts are subject to the same CAN-SPAM
            requirements as manually composed emails
          </li>
        </ul>
        <p>
          Transactional emails (account confirmations, payment receipts) are
          exempt from marketing opt-out requirements per FTC guidance.
        </p>
      </Section>

      <Section title="6. Google API Data -- Limited Use Disclosure">
        <p>
          When you connect your Google account, you grant Real Estate Star
          access to specific Google services. Our use of data obtained through
          Google APIs complies with the{" "}
          <a
            href="https://developers.google.com/terms/api-services-user-data-policy"
            className="text-emerald-400 underline hover:text-emerald-300"
            target="_blank"
            rel="noopener noreferrer"
          >
            Google API Services User Data Policy
          </a>
          , including the Limited Use requirements.
        </p>
        <ul>
          <li>
            We access your Google account <strong>only</strong> to send emails,
            store CMA reports in Drive, and maintain lead tracking sheets on
            your behalf
          </li>
          <li>
            We do <strong>not</strong> read, scan, or index emails you did not
            originate through the Platform
          </li>
          <li>
            We do <strong>not</strong> access Drive files or folders outside of
            those we create
          </li>
          <li>
            We do <strong>not</strong> use Google data to serve advertisements
          </li>
        </ul>
        <p>
          You may revoke Google access at any time via{" "}
          <a
            href="https://myaccount.google.com/permissions"
            className="text-emerald-400 underline hover:text-emerald-300"
            target="_blank"
            rel="noopener noreferrer"
          >
            myaccount.google.com/permissions
          </a>
          . Google OAuth tokens are stored encrypted and automatically deleted
          when your trial expires or your account is terminated.
        </p>
      </Section>

      <Section title="7. Data Sharing">
        <p>We share data only in the following circumstances:</p>
        <ul>
          <li>
            <strong>Service providers:</strong> Stripe (payments), Cloudflare
            (hosting), email providers -- under data processing agreements
          </li>
          <li>
            <strong>Legal requirements:</strong> If required by law, court
            order, or government request
          </li>
          <li>
            <strong>Business transfer:</strong> In connection with a merger,
            acquisition, or sale of assets
          </li>
          <li>
            <strong>With your consent:</strong> For any other purpose, only with
            your explicit consent
          </li>
        </ul>
        <p>
          We do <strong>not</strong> sell, rent, or trade your personal data.
        </p>
      </Section>

      <Section title="8. Data Retention">
        <div className="overflow-x-auto mb-4">
          <table className="w-full text-sm text-gray-300 border border-gray-700">
            <thead>
              <tr>
                <th className="px-4 py-2 bg-gray-800 text-white font-semibold border border-gray-700 text-left">
                  Data Type
                </th>
                <th className="px-4 py-2 bg-gray-800 text-white font-semibold border border-gray-700 text-left">
                  Retention Period
                </th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td className="px-4 py-2 border border-gray-700">
                  Active account data
                </td>
                <td className="px-4 py-2 border border-gray-700">
                  Duration of subscription
                </td>
              </tr>
              <tr>
                <td className="px-4 py-2 border border-gray-700">
                  Session and onboarding data
                </td>
                <td className="px-4 py-2 border border-gray-700">
                  30 days after account termination
                </td>
              </tr>
              <tr>
                <td className="px-4 py-2 border border-gray-700">
                  Google OAuth tokens
                </td>
                <td className="px-4 py-2 border border-gray-700">
                  Deleted at trial expiry or account termination
                </td>
              </tr>
              <tr>
                <td className="px-4 py-2 border border-gray-700">
                  Payment records
                </td>
                <td className="px-4 py-2 border border-gray-700">
                  7 years (tax/legal compliance)
                </td>
              </tr>
              <tr>
                <td className="px-4 py-2 border border-gray-700">
                  Anonymized usage data
                </td>
                <td className="px-4 py-2 border border-gray-700">
                  Indefinitely
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </Section>

      <Section title="9. Data Security">
        <p>We implement industry-standard security measures including:</p>
        <ul>
          <li>Encryption of data at rest and in transit (TLS)</li>
          <li>Encrypted storage of OAuth tokens</li>
          <li>
            Logical isolation of tenant data (your data is not accessible to
            other agents on the Platform)
          </li>
        </ul>
        <p>
          No system is completely secure. In the event of a data breach
          affecting your personal information, we will notify you as required by
          applicable law (generally within 72 hours of discovery).
        </p>
      </Section>

      <Section title="10. California Privacy Rights (CCPA)">
        <p>
          If you are a California resident, you have the following rights under
          the California Consumer Privacy Act:
        </p>
        <ul>
          <li>
            <strong>Right to Know:</strong> Request disclosure of the categories
            and specific pieces of personal information we have collected about
            you
          </li>
          <li>
            <strong>Right to Delete:</strong> Request deletion of your personal
            information, subject to certain exceptions
          </li>
          <li>
            <strong>Right to Correct:</strong> Request correction of inaccurate
            personal information
          </li>
          <li>
            <strong>Right to Opt Out of Sale:</strong> We do not sell personal
            information
          </li>
          <li>
            <strong>Right to Non-Discrimination:</strong> We will not
            discriminate against you for exercising any of these rights
          </li>
        </ul>
        <p>
          To exercise your rights, contact us at{" "}
          <a
            href="mailto:privacy@real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            privacy@real-estate-star.com
          </a>
          . We will respond within 45 days.
        </p>
      </Section>

      {/* TODO: Make state-specific when multi-state support is added (currently NJ-only) */}
      <Section title="11. New Jersey Privacy Rights">
        <p>
          If you are a New Jersey resident, under the New Jersey Data Privacy
          Act (N.J.S.A. 56:8-166), you have the right to confirm, access,
          correct, delete, and obtain a portable copy of your personal data.
          You also have the right to opt out of the sale of personal data,
          targeted advertising, and profiling. We honor browser-based opt-out
          signals (Global Privacy Control).
        </p>
        <p>
          To exercise your rights, contact us at{" "}
          <a
            href="mailto:privacy@real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            privacy@real-estate-star.com
          </a>
          . We will respond within 45 days.
        </p>
      </Section>

      <Section title="12. Children's Privacy">
        <p>
          The Platform is intended solely for licensed real estate professionals
          (adults 18+). We do not knowingly collect personal information from
          anyone under 18. If you believe a minor has submitted information to
          us, contact us and we will delete it promptly.
        </p>
      </Section>

      <Section title="13. Third-Party Links and Services">
        <p>
          Your deployed agent website may contain links to third-party sites
          (MLS, brokerage sites, etc.). This Privacy Policy does not apply to
          those sites. We encourage you to review the privacy policies of any
          third-party services you use.
        </p>
      </Section>

      <Section title="14. Changes to This Policy">
        <p>
          We may update this Privacy Policy from time to time. We will notify
          you of material changes via email or in-app notice at least 14 days
          before they take effect. Continued use of the Platform after the
          effective date constitutes acceptance.
        </p>
      </Section>

      <Section title="15. Contact">
        <p>For privacy questions, data requests, or concerns:</p>
        <p>
          <strong>Real Estate Star</strong>
          <br />
          Email:{" "}
          <a
            href="mailto:privacy@real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            privacy@real-estate-star.com
          </a>
          <br />
          Website:{" "}
          <Link
            href="/privacy"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            real-estate-star.com/privacy
          </Link>
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
      <h2 className="text-xl font-semibold text-white mb-4 border-b border-gray-700 pb-2">
        {title}
      </h2>
      <div className="text-base text-gray-300 leading-relaxed space-y-4 [&_ul]:list-disc [&_ul]:ml-6 [&_ul]:space-y-2 [&_strong]:text-white [&_strong]:font-semibold">
        {children}
      </div>
    </section>
  );
}
