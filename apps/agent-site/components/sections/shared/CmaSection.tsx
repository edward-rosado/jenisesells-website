"use client";

import * as Sentry from "@sentry/nextjs";
import { useCmaSubmit } from "@real-estate-star/ui";
import type { ContactFormData, AccountTracking } from "@/lib/types";
import { trackCmaConversion } from "@/components/Analytics";
import { LeadForm } from "@real-estate-star/ui";
import type { LeadFormData } from "@real-estate-star/shared-types";
import type { ReactNode } from "react";
import { Fragment } from "react";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";

interface CmaSectionProps {
  agentId: string;
  agentName: string;
  defaultState: string;
  tracking?: AccountTracking;
  data: ContactFormData;
  serviceAreas?: string[];
}

export function CmaSection({
  agentId,
  agentName,
  defaultState,
  tracking,
  data,
  serviceAreas = [],
}: CmaSectionProps) {
  const { state, submit } = useCmaSubmit(API_BASE_URL, {
    onError: (err) => {
      Sentry.captureException(err, {
        tags: { agentId, feature: "contact_form" },
      });
    },
  });

  const isProcessing = state.phase === "submitting";

  async function handleSubmit(leadData: LeadFormData) {
    const isSelling = leadData.leadTypes.includes("selling") && leadData.seller?.address;

    if (isSelling) {
      const success = await submit(agentId, leadData);
      if (!success) return;
    }

    trackCmaConversion(tracking);
    const emailParam = leadData.email ? `&email=${encodeURIComponent(leadData.email)}` : "";
    window.location.href = `/thank-you?agentId=${encodeURIComponent(agentId)}${emailParam}`;
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
          error={state.errorMessage ?? undefined}
          serviceAreas={serviceAreas}
          showCmaDisclaimer
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
