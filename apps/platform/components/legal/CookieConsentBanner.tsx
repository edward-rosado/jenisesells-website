"use client";

import { useState, useEffect } from "react";

const COOKIE_CONSENT_KEY = "res-cookie-consent";

export function CookieConsentBanner() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const consent = localStorage.getItem(COOKIE_CONSENT_KEY);
    if (!consent) {
      setVisible(true);
    }
  }, []);

  function handleAccept() {
    localStorage.setItem(COOKIE_CONSENT_KEY, "accepted");
    setVisible(false);
  }

  function handleDecline() {
    localStorage.setItem(COOKIE_CONSENT_KEY, "declined");
    setVisible(false);
  }

  if (!visible) {
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
