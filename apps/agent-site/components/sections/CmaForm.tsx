"use client";

import * as Sentry from "@sentry/nextjs";
import { useState, useRef } from "react";
import type { CmaFormData, AgentTracking } from "@/lib/types";
import { trackCmaConversion } from "@/components/Analytics";
import { useCmaSubmit } from "@/lib/useCmaSubmit";

interface CmaFormProps {
  agentId: string;
  agentName: string;
  defaultState: string;
  formHandler?: "formspree" | "custom";
  formHandlerId?: string;
  tracking?: AgentTracking;
  data: CmaFormData;
}

export function CmaForm({
  agentId,
  agentName,
  defaultState,
  formHandler,
  formHandlerId,
  tracking,
  data,
}: CmaFormProps) {
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);
  const cmaSubmit = useCmaSubmit();

  const isApiMode = formHandler !== "formspree";
  const isProcessing =
    submitting ||
    cmaSubmit.state.phase === "submitting" ||
    cmaSubmit.state.phase === "tracking";

  async function handleFormspreeSubmit(form: HTMLFormElement) {
    setSubmitting(true);
    setError(null);
    const formData = new FormData(form);
    const endpoint = `https://formspree.io/f/${formHandlerId}`;

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

  async function handleApiSubmit(form: HTMLFormElement) {
    const formData = new FormData(form);
    // FormData.get() returns a string for named text inputs present in the form
    const field = (name: string): string => formData.get(name) as string;

    const notes = field("notes");
    const beds = field("beds");
    const baths = field("baths");
    const sqft = field("sqft");
    const request = {
      firstName: field("firstName"),
      lastName: field("lastName"),
      email: field("email"),
      phone: field("phone"),
      address: field("address"),
      city: field("city"),
      state: field("state"),
      zip: field("zip"),
      beds: beds.length > 0 ? Number(beds) : undefined,
      baths: baths.length > 0 ? Number(baths) : undefined,
      sqft: sqft.length > 0 ? Number(sqft) : undefined,
      timeline: field("timeline"),
      notes: notes.length > 0 ? notes : undefined,
    };

    await cmaSubmit.submit(agentId, request);
    trackCmaConversion(tracking);
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = e.currentTarget;

    if (isApiMode) {
      await handleApiSubmit(form);
    } else {
      await handleFormspreeSubmit(form);
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

  const inputStyle: React.CSSProperties = {
    width: "100%",
    padding: "12px 16px",
    border: "2px solid #e0e0e0",
    borderRadius: "8px",
    fontSize: "15px",
    transition: "border 0.3s",
    outline: "none",
    boxSizing: "border-box",
  };

  const labelStyle: React.CSSProperties = {
    display: "block",
    fontSize: "14px",
    fontWeight: 600,
    color: "#333",
    marginBottom: "5px",
  };

  const formGroupStyle: React.CSSProperties = {
    marginBottom: "18px",
  };

  return (
    <div
      id="cma-form"
      style={{
        background: "linear-gradient(135deg, #E8F5E9, #C8E6C9)",
        padding: "70px 40px",
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

        {displayError && (
          <p
            style={{
              color: "#d32f2f",
              textAlign: "center",
              marginBottom: "16px",
              fontWeight: 500,
            }}
          >
            {displayError}
          </p>
        )}

        <div
          style={{
            background: "white",
            borderRadius: "16px",
            padding: "40px",
            boxShadow: "0 10px 40px rgba(0,0,0,0.1)",
            maxWidth: "600px",
            margin: "0 auto",
            textAlign: "left",
          }}
        >
          <h3
            style={{
              textAlign: "center",
              color: "#1B5E20",
              fontSize: "22px",
              marginBottom: "25px",
            }}
          >
            Request Your Free Home Value Report
          </h3>

          <form ref={formRef} onSubmit={handleSubmit}>
            <div style={{ display: "flex", gap: "15px" }}>
              <div style={{ ...formGroupStyle, flex: 1 }}>
                <label htmlFor="firstName" style={labelStyle}>
                  First Name <span style={{ color: "#d32f2f" }}>*</span>
                </label>
                <input
                  id="firstName"
                  name="firstName"
                  placeholder="John"
                  required
                  autoComplete="given-name"
                  style={inputStyle}
                />
              </div>
              <div style={{ ...formGroupStyle, flex: 1 }}>
                <label htmlFor="lastName" style={labelStyle}>
                  Last Name <span style={{ color: "#d32f2f" }}>*</span>
                </label>
                <input
                  id="lastName"
                  name="lastName"
                  placeholder="Smith"
                  required
                  autoComplete="family-name"
                  style={inputStyle}
                />
              </div>
            </div>

            <div style={formGroupStyle}>
              <label htmlFor="email" style={labelStyle}>
                Email Address <span style={{ color: "#d32f2f" }}>*</span>
              </label>
              <input
                id="email"
                name="email"
                type="email"
                placeholder="you@email.com"
                required
                style={inputStyle}
              />
            </div>

            <div style={formGroupStyle}>
              <label htmlFor="phone" style={labelStyle}>
                Phone Number <span style={{ color: "#d32f2f" }}>*</span>
              </label>
              <input
                id="phone"
                name="phone"
                type="tel"
                placeholder="(555) 123-4567"
                required
                style={inputStyle}
              />
            </div>

            <div style={formGroupStyle}>
              <label htmlFor="address" style={labelStyle}>
                Property Address <span style={{ color: "#d32f2f" }}>*</span>
              </label>
              <input
                id="address"
                name="address"
                placeholder="Start typing your address..."
                autoComplete="off"
                required
                style={inputStyle}
              />
            </div>

            <div style={{ display: "flex", gap: "15px" }}>
              <div style={{ ...formGroupStyle, flex: 2 }}>
                <label htmlFor="city" style={labelStyle}>
                  City <span style={{ color: "#d32f2f" }}>*</span>
                </label>
                <input
                  id="city"
                  name="city"
                  placeholder="City"
                  required
                  autoComplete="address-level2"
                  style={inputStyle}
                />
              </div>
              <div style={{ ...formGroupStyle, flex: 1 }}>
                <label htmlFor="state" style={labelStyle}>
                  State <span style={{ color: "#d32f2f" }}>*</span>
                </label>
                <input
                  id="state"
                  name="state"
                  placeholder={defaultState}
                  defaultValue={defaultState}
                  required
                  autoComplete="address-level1"
                  maxLength={2}
                  style={inputStyle}
                />
              </div>
              <div style={{ ...formGroupStyle, flex: 1 }}>
                <label htmlFor="zip" style={labelStyle}>
                  Zip <span style={{ color: "#d32f2f" }}>*</span>
                </label>
                <input
                  id="zip"
                  name="zip"
                  placeholder="08xxx"
                  required
                  autoComplete="postal-code"
                  maxLength={5}
                  style={inputStyle}
                />
              </div>
            </div>

            <div style={{ display: "flex", gap: "15px" }}>
              <div style={{ ...formGroupStyle, flex: 1 }}>
                <label htmlFor="beds" style={labelStyle}>
                  Beds <span style={{ color: "#999", fontSize: "11px" }}>(optional)</span>
                </label>
                <input
                  id="beds"
                  name="beds"
                  type="number"
                  placeholder="3"
                  min={0}
                  max={20}
                  style={inputStyle}
                />
              </div>
              <div style={{ ...formGroupStyle, flex: 1 }}>
                <label htmlFor="baths" style={labelStyle}>
                  Baths <span style={{ color: "#999", fontSize: "11px" }}>(optional)</span>
                </label>
                <input
                  id="baths"
                  name="baths"
                  type="number"
                  placeholder="2"
                  min={0}
                  max={20}
                  step={0.5}
                  style={inputStyle}
                />
              </div>
              <div style={{ ...formGroupStyle, flex: 1 }}>
                <label htmlFor="sqft" style={labelStyle}>
                  Approx Sqft <span style={{ color: "#999", fontSize: "11px" }}>(optional)</span>
                </label>
                <input
                  id="sqft"
                  name="sqft"
                  type="number"
                  placeholder="1,800"
                  min={100}
                  max={50000}
                  style={inputStyle}
                />
              </div>
            </div>

            <div style={formGroupStyle}>
              <label htmlFor="timeline" style={labelStyle}>
                When are you looking to sell? <span style={{ color: "#d32f2f" }}>*</span>
              </label>
              <select
                id="timeline"
                name="timeline"
                required
                style={inputStyle}
              >
                <option value="">Select a timeline...</option>
                <option value="asap">As soon as possible</option>
                <option value="1-3months">1{"\u2013"}3 months</option>
                <option value="3-6months">3{"\u2013"}6 months</option>
                <option value="6-12months">6{"\u2013"}12 months</option>
                <option value="justcurious">
                  Just curious about my home&apos;s value
                </option>
              </select>
            </div>

            <div style={formGroupStyle}>
              <label htmlFor="notes" style={labelStyle}>
                Anything else I should know? <span style={{ color: "#999", fontSize: "11px" }}>(optional)</span>
              </label>
              <textarea
                id="notes"
                name="notes"
                placeholder="Recent upgrades, renovations, special features..."
                rows={2}
                style={inputStyle}
              />
            </div>

            <input
              type="hidden"
              name="_subject"
              value={`New CMA Request — ${agentName}`}
            />

            <button
              type="submit"
              disabled={isProcessing}
              style={{
                display: "block",
                width: "100%",
                background: "#2E7D32",
                color: "white",
                padding: "16px",
                border: "none",
                borderRadius: "30px",
                fontSize: "17px",
                fontWeight: 700,
                cursor: isProcessing ? "not-allowed" : "pointer",
                transition: "background 0.3s",
                opacity: isProcessing ? 0.5 : 1,
              }}
            >
              {isProcessing
                ? "Submitting..."
                : "Get My Free Home Value Report \u2192"}
            </button>
            <p style={{ textAlign: "center", marginTop: "12px", fontSize: "12px", color: "#888" }}>
              {"\uD83D\uDD12"} Your info is secure and never shared.
            </p>
            <p style={{ fontSize: "0.75rem", color: "#999", marginTop: "0.5rem", lineHeight: 1.4, textAlign: "center" }}>
              <em>This home value report is a Comparative Market Analysis (CMA) and is not an appraisal. It should not be considered the equivalent of an appraisal.</em>
            </p>
          </form>
        </div>
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
