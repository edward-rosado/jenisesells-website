"use client";

import { useState, useRef, useEffect } from "react";
import type { SupportedLocale } from "./locale-map";
import { localeToDisplayName } from "./locale-map";

interface LanguageSwitcherProps {
  locales: SupportedLocale[];
  currentLocale: SupportedLocale;
}

function GlobeIcon({ size = 16 }: { size?: number }) {
  return (
    <svg aria-hidden="true" width={size} height={size} fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24" style={{ flexShrink: 0 }}>
      <circle cx="12" cy="12" r="10" />
      <path d="M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10A15.3 15.3 0 0 1 12 2z" />
    </svg>
  );
}

export function LanguageSwitcher({ locales, currentLocale }: LanguageSwitcherProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const hidden = locales.length <= 1;

  // Close on outside click
  useEffect(() => {
    if (!open || hidden) return;
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("click", handleClick);
    return () => document.removeEventListener("click", handleClick);
  }, [open, hidden]);

  // Close on Escape
  useEffect(() => {
    if (!open || hidden) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", handleKey);
    return () => document.removeEventListener("keydown", handleKey);
  }, [open, hidden]);

  if (hidden) return null;

  function handleSelect(locale: SupportedLocale) {
    document.cookie = `locale=${locale};path=/;max-age=${365 * 24 * 60 * 60};samesite=lax`;
    setOpen(false);
    // Reload the page so middleware re-resolves the locale from the cookie
    window.location.reload();
  }

  return (
    <div ref={ref} style={{ position: "relative" }}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-label={`Language: ${localeToDisplayName(currentLocale)}`}
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: "6px",
          background: "rgba(255,255,255,0.1)",
          color: "white",
          padding: "6px 12px",
          borderRadius: "20px",
          border: "1px solid rgba(255,255,255,0.2)",
          cursor: "pointer",
          fontSize: "13px",
          fontWeight: 600,
          whiteSpace: "nowrap",
          minHeight: "36px",
        }}
      >
        <GlobeIcon />
        {localeToDisplayName(currentLocale)}
      </button>

      {open && (
        <div
          role="menu"
          style={{
            position: "absolute",
            top: "calc(100% + 6px)",
            right: 0,
            background: "var(--color-primary, #1B5E20)",
            border: "1px solid rgba(255,255,255,0.2)",
            borderRadius: "10px",
            overflow: "hidden",
            zIndex: 1200,
            minWidth: "140px",
            boxShadow: "0 8px 24px rgba(0,0,0,0.3)",
          }}
        >
          {locales.map((locale) => (
            <button
              key={locale}
              role="menuitem"
              type="button"
              onClick={() => handleSelect(locale)}
              style={{
                display: "block",
                width: "100%",
                padding: "10px 16px",
                border: "none",
                background: locale === currentLocale ? "rgba(255,255,255,0.15)" : "transparent",
                color: "white",
                fontSize: "14px",
                fontWeight: locale === currentLocale ? 700 : 500,
                textAlign: "left",
                cursor: "pointer",
              }}
            >
              {localeToDisplayName(locale)}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
