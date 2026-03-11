import type { Metadata } from "next";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";

export const metadata: Metadata = {
  title: "DMCA Policy | Real Estate Star",
  description:
    "Real Estate Star DMCA Policy -- takedown procedure, counter-notification, and designated agent.",
};

export default function DmcaPage() {
  return (
    <LegalPageLayout>
      <h1 className="text-3xl font-bold text-white mb-2">DMCA Policy</h1>
      <p className="text-sm text-gray-400 mb-8">
        Effective Date: March 11, 2026 | Last Updated: March 11, 2026
      </p>

      <Section title="1. Overview">
        <p>
          Real Estate Star (&quot;we,&quot; &quot;us,&quot; &quot;our&quot;)
          respects the intellectual property rights of others and expects our
          users to do the same. In accordance with the Digital Millennium
          Copyright Act of 1998 (&quot;DMCA&quot;), we will respond promptly to
          claims of copyright infringement committed using the Real Estate Star
          platform.
        </p>
      </Section>

      <Section title="2. Reporting Copyright Infringement">
        <p>
          If you believe that content hosted on our platform infringes your
          copyright, please submit a written notification to our designated DMCA
          agent containing the following information:
        </p>
        <ol className="list-decimal list-inside space-y-2">
          <li>
            A physical or electronic signature of the copyright owner or a
            person authorized to act on their behalf
          </li>
          <li>
            Identification of the copyrighted work claimed to have been
            infringed
          </li>
          <li>
            Identification of the material that is claimed to be infringing,
            with enough detail to allow us to locate it on the platform (e.g.,
            URL)
          </li>
          <li>
            Your contact information, including name, address, telephone number,
            and email address
          </li>
          <li>
            A statement that you have a good faith belief that use of the
            material in the manner complained of is not authorized by the
            copyright owner, its agent, or the law
          </li>
          <li>
            A statement, made under penalty of perjury, that the above
            information is accurate and that you are the copyright owner or
            authorized to act on the copyright owner&apos;s behalf
          </li>
        </ol>
      </Section>

      <Section title="3. Designated DMCA Agent">
        <p>
          Send your DMCA takedown notice to our designated agent:
        </p>
        <div className="bg-gray-800/50 rounded-lg p-4 mt-2">
          <p>
            <strong>DMCA Agent</strong>
            <br />
            Real Estate Star
            <br />
            Email:{" "}
            <a
              href="mailto:dmca@real-estate-star.com"
              className="text-emerald-400 underline hover:text-emerald-300"
            >
              dmca@real-estate-star.com
            </a>
          </p>
        </div>
      </Section>

      <Section title="4. Response to Valid Notices">
        <p>Upon receiving a valid DMCA takedown notice, we will:</p>
        <ol className="list-decimal list-inside space-y-2">
          <li>
            Remove or disable access to the allegedly infringing material
            promptly
          </li>
          <li>
            Notify the user who posted the material that it has been removed or
            disabled
          </li>
          <li>
            Provide the user with a copy of the takedown notice and information
            about how to submit a counter-notification
          </li>
        </ol>
      </Section>

      <Section title="5. Counter-Notification">
        <p>
          If you believe your content was removed by mistake or
          misidentification, you may submit a counter-notification to our DMCA
          agent containing:
        </p>
        <ol className="list-decimal list-inside space-y-2">
          <li>Your physical or electronic signature</li>
          <li>
            Identification of the material that was removed and its location
            before removal
          </li>
          <li>
            A statement under penalty of perjury that you have a good faith
            belief that the material was removed as a result of mistake or
            misidentification
          </li>
          <li>
            Your name, address, telephone number, and a statement that you
            consent to the jurisdiction of the federal court in your district
            and will accept service from the party who submitted the takedown
            notice
          </li>
        </ol>
        <p>
          Upon receiving a valid counter-notification, we will forward it to the
          original complaining party. If the complaining party does not file a
          court action within 10-14 business days, we will restore the removed
          material.
        </p>
      </Section>

      <Section title="6. Repeat Infringers">
        <p>
          In accordance with the DMCA, we maintain a policy of terminating the
          accounts of users who are repeat copyright infringers, in appropriate
          circumstances as determined by us in our sole discretion.
        </p>
      </Section>

      <Section title="7. Good Faith">
        <p>
          Please be aware that filing a false DMCA takedown notice or
          counter-notification may subject you to liability for damages,
          including costs and attorneys&apos; fees. If you are unsure whether
          content infringes your copyright, consider consulting an attorney
          before submitting a notice.
        </p>
      </Section>

      <Section title="8. Contact">
        <p>
          For DMCA-related inquiries:
        </p>
        <p>
          <strong>Real Estate Star</strong>
          <br />
          Email:{" "}
          <a
            href="mailto:dmca@real-estate-star.com"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            dmca@real-estate-star.com
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
      <div className="text-gray-300 leading-relaxed space-y-4 [&_ul]:list-disc [&_ul]:list-inside [&_ul]:space-y-1 [&_ol]:space-y-2 [&_strong]:text-white [&_strong]:font-semibold">
        {children}
      </div>
    </section>
  );
}
