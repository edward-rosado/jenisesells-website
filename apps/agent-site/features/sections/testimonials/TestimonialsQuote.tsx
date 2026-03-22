"use client";

import { useState } from "react";
import { FTC_DISCLAIMER, type TestimonialsProps } from "@/features/sections/types";
import type { TestimonialItem } from "@/features/config/types";

function TestimonialsQuoteCard({ item }: { item: TestimonialItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        textAlign: "center",
        position: "relative",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        padding: "20px",
        borderRadius: "8px",
      }}
    >
      {/* Decorative quotation mark */}
      <div
        data-quotation-mark
        aria-hidden="true"
        style={{
          fontSize: "80px",
          lineHeight: 1,
          color: "var(--color-accent, #b8926a)",
          opacity: 0.2,
          fontFamily: "var(--font-family, Georgia), serif",
          marginBottom: "-24px",
          userSelect: "none",
        }}
      >
        &ldquo;
      </div>
      <p
        role="paragraph"
        style={{
          fontStyle: "italic",
          fontSize: "19px",
          lineHeight: 1.8,
          color: "var(--color-primary, #3d3028)",
          fontFamily: "var(--font-family, Georgia), serif",
          fontWeight: 300,
          marginBottom: "20px",
        }}
      >
        {item.text}
      </p>
      <div
        style={{
          fontSize: "12px",
          letterSpacing: "2px",
          textTransform: "uppercase" as const,
          color: "var(--color-secondary, #5a4a3a)",
          fontVariant: "small-caps",
        }}
      >
        {item.reviewer}
        {item.source && (
          <span
            style={{
              color: "rgba(90,74,58,0.5)",
              fontVariant: "normal",
              textTransform: "none" as const,
            }}
          >
            {" "}via {item.source}
          </span>
        )}
      </div>
    </div>
  );
}

export function TestimonialsQuote({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#ffffff",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "760px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 300,
            color: "var(--color-primary, #3d3028)",
            marginBottom: "60px",
            fontFamily: "var(--font-family, Georgia), serif",
            letterSpacing: "1px",
          }}
        >
          {title ?? "Testimonials"}
        </h2>

        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "56px",
          }}
        >
          {items.map((item) => (
            <TestimonialsQuoteCard key={item.reviewer} item={item} />
          ))}
        </div>

        <p
          style={{
            textAlign: "center",
            color: "#8B7355",
            fontSize: "11px",
            marginTop: "60px",
            lineHeight: 1.6,
          }}
        >
          {FTC_DISCLAIMER}
        </p>
      </div>
    </section>
  );
}
