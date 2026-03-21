"use client";
import { useState, useSyncExternalStore } from "react";

const CONSENT_KEY = "analytics-consent";

function getServerSnapshot() {
  return "pending";
}

function subscribe(callback: () => void) {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
}

function getConsent() {
  return localStorage.getItem(CONSENT_KEY);
}

interface CookieConsentProps {
  /** Only renders when GA4 is configured. Pass measurementId to show the banner. */
  measurementId?: string;
}

export function CookieConsent({ measurementId }: CookieConsentProps) {
  const consent = useSyncExternalStore(subscribe, getConsent, getServerSnapshot);
  const [dismissed, setDismissed] = useState(false);

  // Only show when GA4 is configured
  if (!measurementId) return null;

  // Hide once user has responded or dismissed.
  // consent is null (not set), "pending" (SSR snapshot), "granted", or "denied".
  // Only show banner when consent is truly unset (null from localStorage).
  if (dismissed || consent === "pending" || consent === "granted" || consent === "denied") return null;

  function handleAccept() {
    localStorage.setItem(CONSENT_KEY, "granted");
    setDismissed(true);
  }

  function handleDecline() {
    localStorage.setItem(CONSENT_KEY, "denied");
    setDismissed(true);
  }

  return (
    <div
      role="dialog"
      aria-label="Analytics consent"
      aria-modal="true"
      aria-describedby="analytics-consent-desc"
      className="fixed bottom-0 left-0 right-0 z-40 border-t border-white/20 bg-gray-900 px-6 py-4"
    >
      <div className="mx-auto flex max-w-4xl flex-col items-center gap-4 sm:flex-row sm:justify-between">
        <p id="analytics-consent-desc" className="text-sm text-white/90">
          We use analytics cookies to understand how visitors use this site. Do
          you consent to analytics tracking?
        </p>
        <div className="flex gap-3">
          <button
            onClick={handleDecline}
            className="rounded-lg border border-white/40 px-4 py-2 text-sm text-white/90 transition-colors hover:border-white hover:text-white"
            aria-label="Decline analytics"
          >
            Decline
          </button>
          <button
            onClick={handleAccept}
            className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-gray-900 transition-colors hover:bg-white/90"
            aria-label="Accept analytics"
          >
            Accept
          </button>
        </div>
      </div>
    </div>
  );
}
