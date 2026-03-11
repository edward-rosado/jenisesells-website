import type { Metadata } from "next";
import { LegalPageLayout } from "@/components/legal/LegalPageLayout";

export const metadata: Metadata = {
  title: "Accessibility Statement | Real Estate Star",
  description:
    "Real Estate Star accessibility commitment -- WCAG 2.1 AA compliance and accommodations.",
};

export default function AccessibilityPage() {
  return (
    <LegalPageLayout>
      <h1 className="text-3xl font-bold text-white mb-2">
        Accessibility Statement
      </h1>
      <p className="text-sm text-gray-400 mb-8">
        Effective Date: March 11, 2026 | Last Updated: March 11, 2026
      </p>

      <Section title="Our Commitment">
        <p>
          Real Estate Star is committed to ensuring digital accessibility for
          people with disabilities. We continually improve the user experience
          for everyone and apply the relevant accessibility standards to make
          our platform inclusive.
        </p>
      </Section>

      <Section title="Conformance Status">
        <p>
          We aim to conform to the{" "}
          <a
            href="https://www.w3.org/TR/WCAG21/"
            className="text-emerald-400 underline hover:text-emerald-300"
            target="_blank"
            rel="noopener noreferrer"
          >
            Web Content Accessibility Guidelines (WCAG) 2.1 Level AA
          </a>
          . These guidelines explain how to make web content more accessible to
          people with a wide range of disabilities, including visual, auditory,
          physical, speech, cognitive, language, learning, and neurological
          disabilities.
        </p>
      </Section>

      <Section title="Measures We Take">
        <ul>
          <li>
            <strong>Semantic HTML:</strong> We use proper heading hierarchy,
            landmarks, and ARIA attributes throughout the platform
          </li>
          <li>
            <strong>Keyboard navigation:</strong> All interactive elements are
            accessible via keyboard, with visible focus indicators
          </li>
          <li>
            <strong>Skip navigation:</strong> A &quot;Skip to main content&quot;
            link is provided on every page to help keyboard users bypass
            repeated navigation
          </li>
          <li>
            <strong>Color contrast:</strong> We maintain a minimum contrast
            ratio of 4.5:1 for normal text and 3:1 for large text, meeting WCAG
            AA requirements
          </li>
          <li>
            <strong>Alt text:</strong> All meaningful images include descriptive
            alternative text; decorative images are marked with empty alt
            attributes or aria-hidden
          </li>
          <li>
            <strong>Form labels:</strong> All form controls have associated
            labels, either visible or via screen-reader-only text
          </li>
          <li>
            <strong>Responsive design:</strong> The platform is usable at zoom
            levels up to 200% without loss of content or functionality
          </li>
          <li>
            <strong>Language declaration:</strong> The page language is declared
            in the HTML element to assist screen readers with pronunciation
          </li>
        </ul>
      </Section>

      <Section title="Known Limitations">
        <p>
          Despite our best efforts, some content may not yet be fully
          accessible. We are actively working to identify and resolve
          accessibility barriers. Known areas under improvement include:
        </p>
        <ul>
          <li>
            Third-party embedded content (e.g., Stripe payment forms) may have
            accessibility limitations outside our control
          </li>
          <li>
            Some dynamically generated content (e.g., AI chat responses) may
            require additional screen reader announcements
          </li>
        </ul>
      </Section>

      <Section title="Feedback and Assistance">
        <p>
          We welcome your feedback on the accessibility of Real Estate Star.
          If you encounter accessibility barriers, please contact us:
        </p>
        <div className="bg-gray-800/50 rounded-lg p-4 mt-2">
          <p>
            <strong>Accessibility Contact</strong>
            <br />
            Email:{" "}
            <a
              href="mailto:accessibility@real-estate-star.com"
              className="text-emerald-400 underline hover:text-emerald-300"
            >
              accessibility@real-estate-star.com
            </a>
          </p>
        </div>
        <p>
          We try to respond to accessibility feedback within 5 business days
          and will work with you to provide the information or service you need
          through an accessible alternative.
        </p>
      </Section>

      <Section title="Compatibility">
        <p>
          Real Estate Star is designed to be compatible with the following
          assistive technologies:
        </p>
        <ul>
          <li>Screen readers (NVDA, JAWS, VoiceOver, TalkBack)</li>
          <li>Screen magnification software</li>
          <li>Speech recognition software</li>
          <li>
            Operating system accessibility features (high contrast mode,
            reduced motion)
          </li>
        </ul>
        <p>
          The platform is tested against the latest versions of Chrome, Firefox,
          Safari, and Edge.
        </p>
      </Section>

      <Section title="Legal">
        <p>
          This accessibility statement was prepared on March 11, 2026. We review
          and update this statement as the platform evolves and as we receive
          feedback.
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
