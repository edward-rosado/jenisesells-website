"use client";

import * as Sentry from "@sentry/nextjs";
import { useCmaSubmit } from "@real-estate-star/ui";
import type { CmaFormData, AgentTracking } from "@/lib/types";
import { trackCmaConversion } from "@/components/Analytics";
import { LeadForm } from "@real-estate-star/ui";
import type { LeadFormData } from "@real-estate-star/shared-types";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";

interface CmaSectionProps {
  agentId: string;
  agentName: string;
  defaultState: string;
  tracking?: AgentTracking;
  data: CmaFormData;
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
        tags: { agentId, feature: "cma-form" },
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
      id="cma-form"
      aria-label="Home Value Request Form"
      style={{
        background: "linear-gradient(135deg, #E8F5E9, #C8E6C9)",
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
            color: "#1B5E20",
            marginBottom: "10px",
          }}
        >
          {data.title}
        </h2>
        <p
          style={{
            color: "#C8A951",
            fontSize: "20px",
            fontWeight: 700,
            marginBottom: "15px",
          }}
        >
          {data.subtitle}
        </p>
        <p
          style={{
            color: "#555",
            fontSize: "16px",
            marginBottom: "30px",
          }}
        >
          Fill out the short form below and I&apos;ll send you a personalized Home Value Report showing your home&apos;s estimated market value based on recent comparable sales in your area. <strong>100% free, no obligation.</strong>
        </p>

        <LeadForm
          defaultState={defaultState}
          googleMapsApiKey={process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY ?? ""}
          onSubmit={handleSubmit}
          initialMode={["selling"]}
          agentFirstName={agentName.split(" ")[0]}
          submitLabel={(isBuying, isSelling) => {
            if (isSelling) return "Get My Free Home Value Report \u2192";
            if (isBuying) return `Tell ${agentName.split(" ")[0]} you're ready to buy! \u2192`;
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
