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
    const request = {
      firstName: field("firstName"),
      lastName: field("lastName"),
      email: field("email"),
      phone: field("phone"),
      address: field("address"),
      city: field("city"),
      state: field("state"),
      zip: field("zip"),
      timeline: field("timeline"),
      notes: notes.length > 0 ? notes : undefined,
    };

    trackCmaConversion(tracking);
    await cmaSubmit.submit(agentId, request);
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

  return (
    <section id="cma-form" className="py-16 px-10 max-w-2xl mx-auto">
      <h2
        className="text-3xl font-bold text-center mb-2"
        style={{ color: "var(--color-primary)" }}
      >
        {data.title}
      </h2>
      <p className="text-center text-gray-500 mb-10">{data.subtitle}</p>
      {displayError && (
        <p className="text-red-600 text-center mb-4 font-medium">
          {displayError}
        </p>
      )}
      <form ref={formRef} onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="firstName" className="sr-only">
              First Name
            </label>
            <input
              id="firstName"
              name="firstName"
              placeholder="First Name"
              required
              className="border rounded-lg px-4 py-3 w-full"
            />
          </div>
          <div>
            <label htmlFor="lastName" className="sr-only">
              Last Name
            </label>
            <input
              id="lastName"
              name="lastName"
              placeholder="Last Name"
              required
              className="border rounded-lg px-4 py-3 w-full"
            />
          </div>
        </div>
        <label htmlFor="email" className="sr-only">
          Email Address
        </label>
        <input
          id="email"
          name="email"
          type="email"
          placeholder="Email Address"
          required
          className="border rounded-lg px-4 py-3 w-full"
        />
        <label htmlFor="phone" className="sr-only">
          Phone Number
        </label>
        <input
          id="phone"
          name="phone"
          type="tel"
          placeholder="Phone Number"
          required
          className="border rounded-lg px-4 py-3 w-full"
        />
        <label htmlFor="address" className="sr-only">
          Property Address
        </label>
        <input
          id="address"
          name="address"
          placeholder="Property Address"
          required
          className="border rounded-lg px-4 py-3 w-full"
        />
        <div className="grid grid-cols-3 gap-4">
          <div>
            <label htmlFor="city" className="sr-only">
              City
            </label>
            <input
              id="city"
              name="city"
              placeholder="City"
              required
              className="border rounded-lg px-4 py-3 w-full"
            />
          </div>
          <div>
            <label htmlFor="state" className="sr-only">
              State
            </label>
            <input
              id="state"
              name="state"
              placeholder="State"
              defaultValue={defaultState}
              required
              className="border rounded-lg px-4 py-3 w-full"
            />
          </div>
          <div>
            <label htmlFor="zip" className="sr-only">
              Zip Code
            </label>
            <input
              id="zip"
              name="zip"
              placeholder="Zip"
              required
              className="border rounded-lg px-4 py-3 w-full"
            />
          </div>
        </div>
        <label htmlFor="timeline" className="sr-only">
          When are you looking to sell?
        </label>
        <select
          id="timeline"
          name="timeline"
          required
          className="border rounded-lg px-4 py-3 w-full"
        >
          <option value="">When are you looking to sell?</option>
          <option value="asap">As soon as possible</option>
          <option value="1-3m">1-3 months</option>
          <option value="3-6m">3-6 months</option>
          <option value="6-12m">6-12 months</option>
          <option value="curious">
            Just curious about my home&apos;s value
          </option>
        </select>
        <label htmlFor="notes" className="sr-only">
          Additional notes
        </label>
        <textarea
          id="notes"
          name="notes"
          placeholder="Anything else I should know?"
          rows={3}
          className="border rounded-lg px-4 py-3 w-full"
        />
        <input
          type="hidden"
          name="_subject"
          value={`New CMA Request — ${agentName}`}
        />
        <button
          type="submit"
          disabled={isProcessing}
          className="w-full py-4 rounded-full text-lg font-bold transition-transform hover:-translate-y-0.5 disabled:opacity-50"
          style={{
            backgroundColor: "var(--color-accent)",
            color: "var(--color-primary)",
          }}
        >
          {isProcessing
            ? "Submitting..."
            : "Get My Free Home Value Report \u2192"}
        </button>
      </form>
    </section>
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
