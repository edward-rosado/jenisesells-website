"use client";

import { useState, useSyncExternalStore } from "react";

const COOKIE_CONSENT_KEY = "res-cookie-consent";

function getConsentSnapshot() {
  return localStorage.getItem(COOKIE_CONSENT_KEY);
}

function getServerSnapshot() {
  return "pending";
}

function subscribeToConsent(callback: () => void) {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
}

export function CookieConsentBanner() {
  const consent = useSyncExternalStore(subscribeToConsent, getConsentSnapshot, getServerSnapshot);
  const [dismissed, setDismissed] = useState(false);

  function handleAccept() {
    localStorage.setItem(COOKIE_CONSENT_KEY, "accepted");
    setDismissed(true);
  }

  function handleDecline() {
    localStorage.setItem(COOKIE_CONSENT_KEY, "declined");
    setDismissed(true);
  }

  if (consent || dismissed) {
    return null;
  }

  return (
    <div
      role="dialog"
      aria-label="Cookie consent"
      className="fixed bottom-0 left-0 right-0 z-50 border-t border-gray-800 bg-gray-900 px-6 py-4"
    >
      <div className="mx-auto flex max-w-4xl flex-col items-center gap-4 sm:flex-row sm:justify-between">
        <p className="text-sm text-gray-300">
          We use cookies and local storage to improve your experience. By
          continuing to use this site, you consent to our use of cookies. See
          our{" "}
          <a
            href="/privacy"
            className="text-emerald-400 underline hover:text-emerald-300"
          >
            Privacy Policy
          </a>{" "}
          for details.
        </p>
        <div className="flex gap-3">
          <button
            onClick={handleDecline}
            className="rounded-lg border border-gray-600 px-4 py-2 text-sm text-gray-300 transition-colors hover:border-gray-400 hover:text-white"
            aria-label="Decline cookies"
          >
            Decline
          </button>
          <button
            onClick={handleAccept}
            className="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-emerald-500"
            aria-label="Accept cookies"
          >
            Accept
          </button>
        </div>
      </div>
    </div>
  );
}
