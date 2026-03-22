"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { useReducedMotion } from "@/features/shared/useReducedMotion";
import type { TestimonialsProps } from "@/features/sections/types";
import { clampRating, FTC_DISCLAIMER } from "@/features/sections/types";

const ROTATION_INTERVAL = 5000;

export function TestimonialsSpotlight({ items, title }: TestimonialsProps) {
  const [current, setCurrent] = useState(0);
  const [paused, setPaused] = useState(false);
  const reducedMotion = useReducedMotion();
  const sectionRef = useRef<HTMLElement>(null);

  const goTo = useCallback((index: number) => {
    setCurrent(((index % items.length) + items.length) % items.length);
  }, [items.length]);

  // Auto-rotate
  useEffect(() => {
    if (reducedMotion || paused || items.length <= 1) return;
    const id = setInterval(() => goTo(current + 1), ROTATION_INTERVAL);
    return () => clearInterval(id);
  }, [current, paused, reducedMotion, items.length, goTo]);

  // Pause on focus within
  const handleFocus = useCallback(() => setPaused(true), []);
  const handleBlur = useCallback((e: React.FocusEvent) => {
    if (!sectionRef.current?.contains(e.relatedTarget)) setPaused(false);
  }, []);

  if (items.length === 0) return null;

  const item = items[current];
  const rating = clampRating(item.rating);
  const showNav = items.length > 1;

  const stars = Array.from({ length: 5 }, (_, i) => (
    <span
      key={i}
      data-star
      style={{ color: i < rating ? "#F9A825" : "#E0E0E0", fontSize: "18px" }}
    >
      ★
    </span>
  ));

  return (
    <section
      id="testimonials"
      ref={sectionRef}
      onFocus={handleFocus}
      onBlur={handleBlur}
      style={{
        padding: "80px 24px",
        background: "var(--color-bg, #fafaf8)",
        textAlign: "center" as const,
      }}
    >
      {title && (
        <h2 style={{
          fontSize: "28px",
          fontWeight: 700,
          marginBottom: "40px",
          color: "var(--color-text, #1a1a1a)",
          fontFamily: "var(--font-family, inherit)",
        }}>
          {title}
        </h2>
      )}

      <div
        style={{
          fontSize: "72px",
          color: "rgba(0,0,0,0.06)",
          fontFamily: "Georgia, serif",
          lineHeight: 1,
          marginBottom: "8px",
        }}
      >
        {"\u201C"}
      </div>

      <div aria-live="polite" style={{ minHeight: "160px" }}>
        <p style={{
          fontSize: "20px",
          lineHeight: 1.7,
          fontStyle: "italic" as const,
          color: "var(--color-text, #333)",
          maxWidth: "600px",
          margin: "0 auto 24px",
          overflowWrap: "break-word" as const,
          transition: "opacity 0.6s ease",
        }}>
          {item.text}
        </p>

        <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: "12px" }}>
          <div style={{
            width: "48px",
            height: "48px",
            borderRadius: "50%",
            background: "#e0e0e0",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: "16px",
            color: "#999",
            fontWeight: 600,
          }}>
            {item.reviewer.split(" ").map((w) => w[0]).join("").slice(0, 2).toUpperCase()}
          </div>
          <div style={{ textAlign: "left" as const }}>
            <div style={{ fontWeight: 600, fontSize: "15px", color: "var(--color-text, #333)" }}>
              {item.reviewer}
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
              {item.source && (
                <span style={{ color: "#999", fontSize: "12px" }}>{item.source} · </span>
              )}
              <span>{stars}</span>
            </div>
          </div>
        </div>
      </div>

      {showNav && (
        <div
          role="tablist"
          onKeyDown={(e) => {
            if (e.key === "ArrowRight") goTo(current + 1);
            if (e.key === "ArrowLeft") goTo(current - 1);
          }}
          style={{
            display: "flex",
            justifyContent: "center",
            gap: "8px",
            marginTop: "24px",
          }}
        >
          {items.map((_, i) => (
            <button
              key={i}
              role="tab"
              aria-selected={i === current}
              onClick={() => goTo(i)}
              style={{
                width: "10px",
                height: "10px",
                borderRadius: "50%",
                border: "none",
                background: i === current ? "var(--color-primary, #333)" : "#ddd",
                cursor: "pointer",
                padding: 0,
                transition: "background 0.3s",
              }}
            />
          ))}
        </div>
      )}

      <p style={{
        fontSize: "11px",
        color: "rgba(0,0,0,0.4)",
        maxWidth: "500px",
        margin: "24px auto 0",
        lineHeight: 1.6,
      }}>
        {FTC_DISCLAIMER}
      </p>
    </section>
  );
}
