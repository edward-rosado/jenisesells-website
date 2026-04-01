"use client";

import { useState, useRef, useEffect } from "react";
import { localeToLanguage } from "./locale-map";

interface LanguageSwitcherProps {
  languages: string[];
  currentLocale: string;
}

function GlobeIcon() {
  return (
    <svg
      aria-hidden="true"
      width={18}
      height={18}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
    >
      <circle cx="12" cy="12" r="10" />
      <path d="M2 12h20" />
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
    </svg>
  );
}

export function LanguageSwitcher({ languages, currentLocale }: LanguageSwitcherProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Close dropdown on outside click
  useEffect(() => {
    if (!open) return;
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [open]);

  // Close on Escape
  useEffect(() => {
    if (!open) return;
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open]);

  // Only render when agent supports multiple languages
  if (languages.length <= 1) return null;

  function handleSelect(locale: string) {
    if (locale === currentLocale) {
      setOpen(false);
      return;
    }
    // Set cookie and reload to get fresh server render with new locale
    document.cookie = `locale=${locale}; path=/; max-age=${365 * 24 * 60 * 60}; SameSite=Lax`;
    window.location.reload();
  }

  return (
    <div ref={containerRef} style={{ position: "relative" }}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-haspopup="listbox"
        aria-label="Select language"
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
        {localeToLanguage(currentLocale)}
      </button>

      {open && (
        <ul
          role="listbox"
          aria-label="Available languages"
          style={{
            position: "absolute",
            top: "calc(100% + 4px)",
            right: 0,
            background: "var(--color-primary)",
            border: "1px solid rgba(255,255,255,0.2)",
            borderRadius: "8px",
            padding: "4px 0",
            margin: 0,
            listStyle: "none",
            minWidth: "140px",
            boxShadow: "0 4px 12px rgba(0,0,0,0.3)",
            zIndex: 1200,
          }}
        >
          {languages.map((locale) => (
            <li key={locale} role="option" aria-selected={locale === currentLocale}>
              <button
                type="button"
                onClick={() => handleSelect(locale)}
                style={{
                  display: "block",
                  width: "100%",
                  textAlign: "left",
                  padding: "8px 16px",
                  background: locale === currentLocale ? "rgba(255,255,255,0.15)" : "transparent",
                  color: "white",
                  border: "none",
                  cursor: "pointer",
                  fontSize: "14px",
                  fontWeight: locale === currentLocale ? 700 : 400,
                }}
                onMouseEnter={(e) => { e.currentTarget.style.background = "rgba(255,255,255,0.1)"; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = locale === currentLocale ? "rgba(255,255,255,0.15)" : "transparent"; }}
              >
                {localeToLanguage(locale)}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
