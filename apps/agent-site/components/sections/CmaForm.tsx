"use client";

import * as Sentry from "@sentry/nextjs";
import { useState } from "react";
import type { CmaFormData, AgentTracking } from "@/lib/types";
import { trackCmaConversion } from "@/components/Analytics";
import { useCmaSubmit } from "@/lib/useCmaSubmit";
import { LeadForm } from "@real-estate-star/ui";
import type { LeadFormData } from "@real-estate-star/shared-types";

interface CmaFormProps {
  agentId: string;
  agentName: string;
  defaultState: string;
  formHandler?: "formspree" | "custom";
  formHandlerId?: string;
  tracking?: AgentTracking;
  data: CmaFormData;
  serviceAreas?: string[];
}

export function CmaForm({
  agentId,
  agentName,
  defaultState,
  formHandler,
  formHandlerId,
  tracking,
  data,
  serviceAreas = [],
}: CmaFormProps) {
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const cmaSubmit = useCmaSubmit();

  const isApiMode = formHandler !== "formspree";
  const isProcessing =
    submitting ||
    cmaSubmit.state.phase === "submitting" ||
    cmaSubmit.state.phase === "tracking";

  async function handleFormspreeSubmit(leadData: LeadFormData) {
    setSubmitting(true);
    setError(null);
    const endpoint = `https://formspree.io/f/${formHandlerId}`;

    const formData = new FormData();
    formData.set("firstName", leadData.firstName);
    formData.set("lastName", leadData.lastName);
    formData.set("email", leadData.email);
    formData.set("phone", leadData.phone);
    if (leadData.seller) {
      formData.set("address", leadData.seller.address);
      formData.set("city", leadData.seller.city);
      formData.set("state", leadData.seller.state);
      formData.set("zip", leadData.seller.zip);
      if (leadData.seller.beds !== undefined) formData.set("beds", String(leadData.seller.beds));
      if (leadData.seller.baths !== undefined) formData.set("baths", String(leadData.seller.baths));
      if (leadData.seller.sqft !== undefined) formData.set("sqft", String(leadData.seller.sqft));
    }
    formData.set("timeline", leadData.timeline);
    if (leadData.notes) formData.set("notes", leadData.notes);

    try {
      const response = await fetch(endpoint, {
        method: "POST",
        body: formData,
        headers: { Accept: "application/json" },
      });
      if (!response.ok) {
        throw new Error(`Submission failed (${response.status})`);
      }
      trackCmaConversion(tracking);
      window.location.href = `/thank-you?agentId=${encodeURIComponent(agentId)}`;
    } catch (err) {
      Sentry.captureException(err, {
        tags: { agentId, feature: "cma-form" },
        extra: { endpoint },
      });
      setError("Something went wrong. Please try again.");
      setSubmitting(false);
    }
  }

  async function handleApiSubmit(leadData: LeadFormData) {
    const request = {
      firstName: leadData.firstName,
      lastName: leadData.lastName,
      email: leadData.email,
      phone: leadData.phone,
      address: leadData.seller?.address ?? "",
      city: leadData.seller?.city ?? "",
      state: leadData.seller?.state ?? "",
      zip: leadData.seller?.zip ?? "",
      beds: leadData.seller?.beds,
      baths: leadData.seller?.baths,
      sqft: leadData.seller?.sqft,
      timeline: leadData.timeline,
      notes: leadData.notes,
    };

    await cmaSubmit.submit(agentId, request);
    trackCmaConversion(tracking);
  }

  async function handleSubmit(leadData: LeadFormData) {
    if (isApiMode) {
      await handleApiSubmit(leadData);
    } else {
      await handleFormspreeSubmit(leadData);
    }
  }

  // Show progress tracker for API mode when tracking or complete
  if (
    isApiMode &&
    (cmaSubmit.state.phase === "tracking" ||
      cmaSubmit.state.phase === "complete" ||
      (cmaSubmit.state.phase === "error" && cmaSubmit.state.statusUpdate))
  ) {
    return (
      <section id="cma-form" className="py-16 px-10 max-w-2xl mx-auto">
        <h2
          className="text-3xl font-bold text-center mb-2"
          style={{ color: "var(--color-primary)" }}
        >
          {cmaSubmit.state.phase === "complete"
            ? "Your Report Is Ready!"
            : cmaSubmit.state.phase === "error"
              ? "Something Went Wrong"
              : "Preparing Your Report..."}
        </h2>
        <div className="mt-8">
          <ProgressTracker
            step={cmaSubmit.state.statusUpdate?.step ?? 0}
            totalSteps={cmaSubmit.state.statusUpdate?.totalSteps ?? 9}
            message={cmaSubmit.state.statusUpdate?.message ?? "Starting..."}
            phase={cmaSubmit.state.phase}
          />
          {cmaSubmit.state.phase === "complete" && (
            <div className="text-center mt-8">
              <p className="text-lg text-gray-600 mb-4">
                Your personalized Comparative Market Analysis has been sent to your email.
                Check your inbox!
              </p>
              <a
                href={`/thank-you?agentId=${encodeURIComponent(agentId)}`}
                className="inline-block px-8 py-3 rounded-full font-bold transition-transform hover:-translate-y-0.5"
                style={{
                  backgroundColor: "var(--color-accent)",
                  color: "var(--color-primary)",
                }}
              >
                Back to {agentName}&apos;s Site
              </a>
            </div>
          )}
          {cmaSubmit.state.phase === "error" && (
            <div className="text-center mt-8">
              <p className="text-red-600 font-medium mb-4">
                {cmaSubmit.state.errorMessage}
              </p>
              <button
                type="button"
                onClick={() => cmaSubmit.reset()}
                className="inline-block px-8 py-3 rounded-full font-bold transition-transform hover:-translate-y-0.5"
                style={{
                  backgroundColor: "var(--color-accent)",
                  color: "var(--color-primary)",
                }}
              >
                Try Again
              </button>
            </div>
          )}
        </div>
      </section>
    );
  }

  // Show error from API submission (before SignalR connection)
  const displayError =
    error ??
    (cmaSubmit.state.phase === "error" ? cmaSubmit.state.errorMessage : null);

  return (
    <div
      id="cma-form"
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
          submitLabel={(isBuying, isSelling) => {
            if (isSelling) return "Get My Free Home Value Report \u2192";
            if (isBuying) return "Connect Me With an Agent \u2192";
            return "Get Started \u2192";
          }}
          disabled={isProcessing}
          error={displayError ?? undefined}
          serviceAreas={serviceAreas}
        />
      </div>
    </div>
  );
}

// --- Progress Tracker sub-component ---

interface ProgressTrackerProps {
  step: number;
  totalSteps: number;
  message: string;
  phase: "tracking" | "complete" | "error";
}

function ProgressTracker({
  step,
  totalSteps,
  message,
  phase,
}: ProgressTrackerProps) {
  const percentage =
    phase === "complete" ? 100 : Math.round((step / totalSteps) * 100);

  return (
    <div className="space-y-4">
      {/* Progress bar */}
      <div className="w-full bg-gray-200 rounded-full h-3 overflow-hidden">
        <div
          className="h-full rounded-full transition-all duration-500 ease-out"
          style={{
            width: `${percentage}%`,
            backgroundColor:
              phase === "error"
                ? "#dc2626"
                : phase === "complete"
                  ? "#16a34a"
                  : "var(--color-accent)",
          }}
          role="progressbar"
          aria-valuenow={percentage}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label="CMA report progress"
        />
      </div>

      {/* Status text */}
      <p className="text-center text-gray-700 font-medium" data-testid="progress-message">
        {message}
      </p>

      {/* Step counter */}
      <p className="text-center text-sm text-gray-500">
        Step {step} of {totalSteps}
      </p>
    </div>
  );
}
