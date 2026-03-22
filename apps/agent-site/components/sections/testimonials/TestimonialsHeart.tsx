"use client";

import { useState } from "react";
import { clampRating, FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";
import type { TestimonialItem } from "@/features/config/types";

function TestimonialCard({ item }: { item: TestimonialItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "white",
        borderRadius: "20px",
        padding: "32px 28px",
        boxShadow: hover
          ? "0 8px 28px rgba(90,158,124,0.22)"
          : "0 4px 16px rgba(90,158,124,0.1)",
        position: "relative",
        transition: "transform 0.3s, box-shadow 0.3s",
        transform: hover ? "translateY(-4px)" : "none",
      }}
    >
      {/* Decorative quotation mark */}
      <span
        aria-hidden="true"
        style={{
          position: "absolute",
          top: "16px",
          right: "20px",
          fontSize: "64px",
          lineHeight: 1,
          color: "var(--color-accent, #5a9e7c)",
          opacity: 0.15,
          fontFamily: "Georgia, serif",
          pointerEvents: "none",
        }}
      >
        &ldquo;
      </span>

      {/* Star rating */}
      <span
        role="img"
        aria-label={`${clampRating(item.rating)} out of 5 stars`}
        style={{
          display: "block",
          color: "var(--color-accent, #5a9e7c)",
          fontSize: "18px",
          marginBottom: "12px",
        }}
      >
        {"★".repeat(clampRating(item.rating))}
        {"☆".repeat(5 - clampRating(item.rating))}
      </span>

      <p
        style={{
          fontSize: "15px",
          color: "#4a6b5a",
          lineHeight: 1.7,
          fontStyle: "italic",
          marginBottom: "16px",
        }}
      >
        {item.text}
      </p>

      <footer
        style={{
          fontSize: "14px",
          fontWeight: 600,
          color: "var(--color-primary, #2d4a3e)",
          fontFamily: "var(--font-family, Nunito), sans-serif",
        }}
      >
        — {item.reviewer}
      </footer>
    </article>
  );
}

export function TestimonialsHeart({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#f0f7f4",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1000px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 600,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: "8px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {title ?? "What Our Clients Say"}
        </h2>
        <p
          style={{
            textAlign: "center",
            color: "#7a9a88",
            fontSize: "11px",
            marginBottom: "48px",
          }}
        >
          {FTC_DISCLAIMER}
        </p>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <TestimonialCard key={item.reviewer} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
