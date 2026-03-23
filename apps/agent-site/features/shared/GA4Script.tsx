"use client";
import { useSyncExternalStore } from "react";
import Script from "next/script";

const CONSENT_KEY = "analytics-consent";

// Sanitize measurement IDs to prevent script injection via config
const SAFE_GA4_ID = /^G-[A-Za-z0-9]+$/;
function safeMeasurementId(value: string | undefined): string | null {
  if (!value || !SAFE_GA4_ID.test(value)) return null;
  return value;
}

function getServerSnapshot() {
  return null;
}

function subscribe(callback: () => void) {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
}

function getConsent() {
  return localStorage.getItem(CONSENT_KEY);
}

interface GA4ScriptProps {
  measurementId?: string;
}

export function GA4Script({ measurementId }: GA4ScriptProps) {
  const safeId = safeMeasurementId(measurementId);

  const consent = useSyncExternalStore(subscribe, getConsent, getServerSnapshot);

  if (!safeId || consent !== "granted") return null;

  return (
    <>
      <Script
        src={`https://www.googletagmanager.com/gtag/js?id=${safeId}`}
        strategy="afterInteractive"
      />
      <Script id="ga4-byoa-init" strategy="afterInteractive">
        {`window.dataLayer=window.dataLayer||[];function gtag(){dataLayer.push(arguments);}gtag('js',new Date());gtag('config','${safeId}');`}
      </Script>
    </>
  );
}
