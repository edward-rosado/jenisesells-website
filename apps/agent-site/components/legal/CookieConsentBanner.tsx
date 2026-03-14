"use client";

import { useState, useSyncExternalStore } from "react";

function getConsentKey(agentId: string) {
  return `res-cookie-consent-${agentId}`;
}

function getServerSnapshot() {
  return "pending";
}

export function CookieConsentBanner({ agentId }: { agentId: string }) {
  const key = getConsentKey(agentId);

  const consent = useSyncExternalStore(
    (callback) => {
      window.addEventListener("storage", callback);
      return () => window.removeEventListener("storage", callback);
    },
    () => localStorage.getItem(key),
    getServerSnapshot
  );

  const [dismissed, setDismissed] = useState(false);

  function handleAccept() {
    localStorage.setItem(key, "accepted");
    setDismissed(true);
  }

  function handleDecline() {
    localStorage.setItem(key, "declined");
    setDismissed(true);
  }

  if (consent || dismissed) {
    return null;
  }

  return (
    <div
      role="dialog"
      aria-label="Cookie consent"
      className="fixed bottom-0 left-0 right-0 z-50 border-t border-white/20 px-6 py-4"
      style={{ backgroundColor: "var(--color-primary)" }}
    >
      <div className="mx-auto flex max-w-4xl flex-col items-center gap-4 sm:flex-row sm:justify-between">
        <p className="text-sm text-white/90">
          We use cookies and local storage to improve your experience. By
          continuing to use this site, you consent to our use of cookies. See
          our{" "}
          <a href="/privacy" className="underline hover:text-white">
            Privacy Policy
          </a>{" "}
          for details.
        </p>
        <div className="flex gap-3">
          <button
            onClick={handleDecline}
            className="rounded-lg border border-white/40 px-4 py-2 text-sm text-white/90 transition-colors hover:border-white hover:text-white"
            aria-label="Decline cookies"
          >
            Decline
          </button>
          <button
            onClick={handleAccept}
            className="rounded-lg px-4 py-2 text-sm font-semibold text-white transition-colors"
            style={{ backgroundColor: "var(--color-accent)" }}
            aria-label="Accept cookies"
          >
            Accept
          </button>
        </div>
      </div>
    </div>
  );
}
