"use client";

import { useState } from "react";
import { clampRating, FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";
import type { TestimonialItem } from "@/lib/types";

function TestimonialsStackCard({ item }: { item: TestimonialItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#fafafa",
        borderRadius: "16px",
        padding: "28px 32px",
        display: "flex",
        gap: "20px",
        alignItems: "flex-start",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      {/* Circle initial avatar */}
      <div
        data-avatar-initial
        aria-hidden="true"
        style={{
          width: "48px",
          height: "48px",
          borderRadius: "50%",
          background: "var(--color-accent, #ff6b6b)",
          color: "white",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: "20px",
          fontWeight: 800,
          flexShrink: 0,
          fontFamily: "var(--font-family, Inter), sans-serif",
        }}
      >
        {item.reviewer[0]}
      </div>

      <div style={{ flex: 1 }}>
        {/* Star rating */}
        <span
          role="img"
          aria-label={`${clampRating(item.rating)} out of 5 stars`}
          style={{
            display: "block",
            color: "var(--color-accent, #ff6b6b)",
            fontSize: "16px",
            marginBottom: "10px",
            letterSpacing: "2px",
          }}
        >
          {"★".repeat(clampRating(item.rating))}
          {"☆".repeat(5 - clampRating(item.rating))}
        </span>

        {/* Quote text */}
        <p
          style={{
            fontSize: "16px",
            lineHeight: 1.7,
            color: "#333",
            marginBottom: "14px",
            fontStyle: "italic",
          }}
        >
          &ldquo;{item.text}&rdquo;
        </p>

        {/* Reviewer + source */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: "12px",
            flexWrap: "wrap",
          }}
        >
          <span
            style={{
              fontSize: "14px",
              fontWeight: 700,
              color: "var(--color-primary, #1a1a1a)",
            }}
          >
            {item.reviewer}
          </span>
          {item.source && (
            <span
              style={{
                fontSize: "11px",
                fontWeight: 600,
                color: "var(--color-accent, #ff6b6b)",
                background: "rgba(255,107,107,0.1)",
                borderRadius: "12px",
                padding: "3px 10px",
                textTransform: "uppercase" as const,
                letterSpacing: "0.5px",
              }}
            >
              {item.source}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

export function TestimonialsStack({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "white",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "800px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 800,
            color: "var(--color-primary, #1a1a1a)",
            marginBottom: "50px",
            fontFamily: "var(--font-family, Inter), sans-serif",
          }}
        >
          {title ?? "Client Reviews"}
        </h2>

        <div
          data-testimonials-stack
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <TestimonialsStackCard key={item.reviewer} item={item} />
          ))}
        </div>

        <p
          style={{
            textAlign: "center",
            color: "#767676",
            fontSize: "11px",
            marginTop: "50px",
            lineHeight: 1.6,
          }}
        >
          {FTC_DISCLAIMER}
        </p>
      </div>
    </section>
  );
}
