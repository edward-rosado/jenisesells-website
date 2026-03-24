"use client";

import { useRef, useEffect, type CSSProperties } from "react";
import type { PlaceSuggestion } from "./useGooglePlacesAutocomplete";

export interface AddressAutocompleteProps {
  query: string;
  setQuery: (value: string) => void;
  suggestions: PlaceSuggestion[];
  highlightedIndex: number;
  setHighlightedIndex: (index: number) => void;
  selectSuggestion: (index: number) => Promise<void>;
  clearSuggestions: () => void;
  fetchError: string | null;
  inputStyle: CSSProperties;
  required?: boolean;
  id?: string;
  disabled?: boolean;
}

const LISTBOX_ID = "address-listbox";

function optionId(index: number): string {
  return `address-option-${index}`;
}

const LocationIcon = () => (
  <svg
    width="16"
    height="16"
    viewBox="0 0 24 24"
    fill="none"
    stroke="#888"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
    style={{ flexShrink: 0 }}
  >
    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
    <circle cx="12" cy="10" r="3" />
  </svg>
);

export function AddressAutocomplete({
  query,
  setQuery,
  suggestions,
  highlightedIndex,
  setHighlightedIndex,
  selectSuggestion,
  clearSuggestions,
  fetchError,
  inputStyle,
  required,
  id = "lf-address",
  disabled,
}: AddressAutocompleteProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const isOpen = suggestions.length > 0;

  // Click outside handler
  useEffect(() => {
    if (!isOpen) return;

    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        clearSuggestions();
      }
    }

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [isOpen, clearSuggestions]);

  function handleKeyDown(e: React.KeyboardEvent) {
    if (!isOpen) return;

    switch (e.key) {
      case "ArrowDown": {
        e.preventDefault();
        const next = highlightedIndex < suggestions.length - 1 ? highlightedIndex + 1 : 0;
        setHighlightedIndex(next);
        break;
      }
      case "ArrowUp": {
        e.preventDefault();
        const prev = highlightedIndex > 0 ? highlightedIndex - 1 : suggestions.length - 1;
        setHighlightedIndex(prev);
        break;
      }
      case "Enter": {
        if (highlightedIndex >= 0) {
          e.preventDefault();
          void selectSuggestion(highlightedIndex).then(() => {
            inputRef.current?.focus();
          });
        }
        break;
      }
      case "Escape": {
        e.preventDefault();
        clearSuggestions();
        inputRef.current?.focus();
        break;
      }
    }
  }

  function handleItemClick(index: number) {
    void selectSuggestion(index).then(() => {
      inputRef.current?.focus();
    });
  }

  return (
    <div ref={containerRef} style={{ position: "relative", width: "100%" }}>
      <span
        style={{
          position: "absolute",
          left: 12,
          top: 0,
          height: inputStyle.padding ? undefined : 46,
          display: "flex",
          alignItems: "center",
          pointerEvents: "none",
          zIndex: 1,
        }}
      >
        <LocationIcon />
      </span>
      <input
        ref={inputRef}
        id={id}
        role="combobox"
        aria-expanded={isOpen}
        aria-controls={LISTBOX_ID}
        aria-autocomplete="list"
        aria-activedescendant={highlightedIndex >= 0 ? optionId(highlightedIndex) : undefined}
        aria-required={required || undefined}
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        onBlur={() => {
          setTimeout(() => {
            if (!containerRef.current?.contains(document.activeElement)) {
              clearSuggestions();
            }
          }, 150);
        }}
        onKeyDown={handleKeyDown}
        required={required}
        disabled={disabled}
        style={{
          ...inputStyle,
          paddingLeft: 36,
          width: "100%",
          boxSizing: "border-box",
        }}
      />

      {isOpen && (
        <div style={{
          position: "absolute",
          top: "100%",
          left: 0,
          right: 0,
          background: "#fff",
          border: "1px solid #e0e0e0",
          borderRadius: 8,
          boxShadow: "0 4px 12px rgba(0,0,0,0.12)",
          zIndex: 1000,
          marginTop: 4,
          overflow: "hidden",
        }}>
          <ul
            id={LISTBOX_ID}
            role="listbox"
            style={{ maxHeight: 220, overflowY: "auto", listStyle: "none", margin: 0, padding: 0 }}
          >
            {suggestions.map((s) => (
              <li
                key={s.placeId || s.index}
                id={optionId(s.index)}
                role="option"
                aria-selected={s.index === highlightedIndex}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => handleItemClick(s.index)}
                onMouseEnter={() => setHighlightedIndex(s.index)}
                style={{
                  padding: "10px 14px",
                  cursor: "pointer",
                  fontSize: 14,
                  background: s.index === highlightedIndex ? "#f0f0f0" : "transparent",
                  borderBottom: "1px solid #f5f5f5",
                  textAlign: "left",
                  listStyle: "none",
                }}
              >
                {s.text}
              </li>
            ))}
          </ul>
          <div style={{ padding: "6px 14px", fontSize: 11, color: "#999", textAlign: "right", background: "#fafafa", borderTop: "1px solid #f0f0f0" }} aria-hidden="true">
            Powered by Google
          </div>
        </div>
      )}

      {fetchError && (
        <p
          role="alert"
          style={{
            color: "#d32f2f",
            fontSize: 12,
            marginTop: 4,
            marginBottom: 0,
          }}
        >
          {fetchError}
        </p>
      )}
    </div>
  );
}
