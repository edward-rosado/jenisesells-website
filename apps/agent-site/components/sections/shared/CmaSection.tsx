"use client";

import * as Sentry from "@sentry/nextjs";
import type { ContactFormData, AccountTracking } from "@/lib/types";
import { trackCmaConversion } from "@/components/Analytics";
import { LeadForm } from "@real-estate-star/ui";
import type { LeadFormData } from "@real-estate-star/shared-types";
import { Turnstile } from "@marsidev/react-turnstile";
import type { ReactNode } from "react";
import { Fragment, useState } from "react";
import { submitLead } from "@/actions/submit-lead";

interface CmaSectionProps {
  accountId: string;
  agentName: string;
  defaultState: string;
  tracking?: AccountTracking;
  data: ContactFormData;
  serviceAreas?: string[];
}

export function CmaSection({
  accountId,
  agentName,
  defaultState,
  tracking,
  data,
  serviceAreas = [],
}: CmaSectionProps) {
  const [isProcessing, setIsProcessing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);

  async function handleSubmit(leadData: LeadFormData) {
    setIsProcessing(true);
    setErrorMessage(null);

    try {
      const result = await submitLead(accountId, leadData, turnstileToken ?? "");

      if (result.error) {
        setErrorMessage(result.error);
        Sentry.captureException(new Error(result.error), {
          tags: { accountId, feature: "contact_form" },
        });
        return;
      }
    } catch (err) {
      Sentry.captureException(err, {
        tags: { accountId, feature: "contact_form" },
      });
      setErrorMessage("Something went wrong. Please try again.");
      return;
    } finally {
      setIsProcessing(false);
    }

    trackCmaConversion(tracking);
    const emailParam = leadData.email ? `&email=${encodeURIComponent(leadData.email)}` : "";
    window.location.href = `/thank-you?accountId=${encodeURIComponent(accountId)}${emailParam}`;
  }

  return (
    <section
      id="contact_form"
      aria-label="Home Value Request Form"
      style={{
        background: "#f7f7f7",
        padding: "70px 20px",
      }}
    >
      <div
        style={{
          maxWidth: "800px",
          margin: "0 auto",
          textAlign: "center",
        }}
      >
        <h2
          style={{
            fontSize: "32px",
            color: "var(--color-primary)",
            marginBottom: "10px",
          }}
        >
          {data.title}
        </h2>
        <p
          style={{
            color: "var(--color-accent)",
            fontSize: "20px",
            fontWeight: 700,
            marginBottom: "15px",
          }}
        >
          {data.subtitle}
        </p>
        {data.description && (
          <p
            style={{
              color: "#555",
              fontSize: "16px",
              marginBottom: "30px",
            }}
          >
            {renderBoldMarkdown(data.description)}
          </p>
        )}

        <LeadForm
          defaultState={defaultState}
          googleMapsApiKey={process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY ?? ""}
          onSubmit={handleSubmit}
          initialMode={["selling"]}
          agentFirstName={agentName.split(" ")[0]}
          submitLabel={(isBuying, isSelling) => {
            if (isSelling) return "Get My Free Home Value Report \u2192";
            if (isBuying) return "Find My Dream Home \u2192";
            return "Get Started \u2192";
          }}
          disabled={isProcessing}
          error={errorMessage ?? undefined}
          serviceAreas={serviceAreas}
          showCmaDisclaimer
          turnstileToken={process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY ? turnstileToken : undefined}
          captchaSlot={
            process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY ? (
              <Turnstile
                siteKey={process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY}
                onSuccess={setTurnstileToken}
              />
            ) : undefined
          }
        />
      </div>
    </section>
  );
}

/** Converts **bold** markdown to <strong> elements in React */
function renderBoldMarkdown(text: string): ReactNode[] {
  const parts = text.split(/(\*\*.+?\*\*)/g);
  return parts.map((part, i) => {
    if (part.startsWith("**") && part.endsWith("**")) {
      return <Fragment key={i}><strong>{part.slice(2, -2)}</strong></Fragment>;
    }
    return <Fragment key={i}>{part}</Fragment>;
  });
}
