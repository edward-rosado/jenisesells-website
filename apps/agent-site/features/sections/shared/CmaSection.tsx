"use client";

import type { ContactFormData, AccountTracking } from "@/features/config/types";
import { trackCmaConversion } from "@/features/shared/Analytics";
import { trackFormEvent, EventType } from "@/features/shared/telemetry";
import { getUiStrings } from "@/features/i18n";
import { LeadForm } from "@real-estate-star/forms";
import type { LeadFormLabels } from "@real-estate-star/forms";
import type { LeadFormData } from "@real-estate-star/domain";
import dynamic from "next/dynamic";

const Turnstile = dynamic(
  () => import("@marsidev/react-turnstile").then((m) => m.Turnstile),
  { ssr: false }
);
import type { ReactNode } from "react";
import { Fragment, useEffect, useRef, useState } from "react";
import { submitLead } from "@/features/lead-capture/submit-lead";

interface CmaSectionProps {
  accountId: string;
  agentName: string;
  defaultState: string;
  tracking?: AccountTracking;
  data: ContactFormData;
  serviceAreas?: string[];
  locale?: string;
}

export function CmaSection({
  accountId,
  agentName,
  defaultState,
  tracking,
  data,
  serviceAreas = [],
  locale,
}: CmaSectionProps) {
  const [isProcessing, setIsProcessing] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);
  const ui = getUiStrings(locale);

  const sectionRef = useRef<HTMLElement>(null);
  const viewedRef = useRef(false);
  const startedRef = useRef(false);

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting && !viewedRef.current) {
            viewedRef.current = true;
            trackFormEvent(EventType.Viewed, accountId);
            observer.disconnect();
          }
        }
      },
      { threshold: 0.25 }
    );

    observer.observe(sectionRef.current!);
    return () => observer.disconnect();
  }, [accountId]);

  function handleFirstFocus() {
    if (!startedRef.current) {
      startedRef.current = true;
      trackFormEvent(EventType.Started, accountId);
    }
  }

  async function handleSubmit(leadData: LeadFormData) {
    trackFormEvent(EventType.Submitted, accountId);
    setIsProcessing(true);
    setErrorMessage(null);

    try {
      const result = await submitLead(accountId, leadData, turnstileToken ?? "");

      if (result.error) {
        setErrorMessage(result.error);
        console.error("[agent-site] Lead submission error:", result.error);
        trackFormEvent(EventType.Failed, accountId, "server_error");
        return;
      }
    } catch (err) {
      console.error("[agent-site] Lead submission failed:", err);
      setErrorMessage(ui.submissionError);
      trackFormEvent(EventType.Failed, accountId, "network_error");
      return;
    } finally {
      setIsProcessing(false);
    }

    trackFormEvent(EventType.Succeeded, accountId);
    trackCmaConversion(tracking);
    window.location.href = `/thank-you?accountId=${encodeURIComponent(accountId)}`;
  }

  return (
    <section
      ref={sectionRef}
      id="contact_form"
      aria-label={ui.formAriaLabel}
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

        <div onFocus={handleFirstFocus}>
          <LeadForm
            defaultState={defaultState}
            googleMapsApiKey={process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY ?? ""}
            onSubmit={handleSubmit}
            initialMode={["selling"]}
            agentFirstName={agentName.split(" ")[0]}
            submitLabel={(isBuying, isSelling) => {
              if (isSelling) return ui.submitSellingLabel;
              if (isBuying) return ui.submitBuyingLabel;
              return ui.submitDefaultLabel;
            }}
            disabled={isProcessing}
            error={errorMessage ?? undefined}
            serviceAreas={serviceAreas}
            showCmaDisclaimer
            labels={ui as unknown as LeadFormLabels}
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
