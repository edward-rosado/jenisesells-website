"use client";

import { useState } from "react";
import {
  clampRating,
  FTC_DISCLAIMER,
  type TestimonialsProps,
} from "@/features/sections/types";
import type { TestimonialItem } from "@/features/config/types";

function TestimonialsRusticCard({ item }: { item: TestimonialItem }) {
  const [hover, setHover] = useState(false);
  const rating = clampRating(item.rating);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "var(--color-bg, #faf6f0)",
        borderRadius: "8px",
        padding: "28px",
        border: "1px solid #e8e2d8",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      <span
        role="img"
        aria-label={`${rating} out of 5 stars`}
        style={{
          display: "block",
          color: "#d4a017",
          fontSize: "16px",
          marginBottom: "14px",
          letterSpacing: "2px",
        }}
      >
        {"★".repeat(rating)}
        {"☆".repeat(5 - rating)}
      </span>

      <p
        style={{
          color: "#5a5040",
          fontSize: "15px",
          lineHeight: 1.75,
          fontStyle: "italic",
          fontFamily: "Georgia, serif",
          marginBottom: "16px",
        }}
      >
        {item.text}
      </p>

      <div
        style={{
          fontWeight: 600,
          color: "var(--color-primary, #2d4a3e)",
          fontSize: "14px",
          fontFamily: "sans-serif",
        }}
      >
        — {item.reviewer}
        {item.source && (
          <span
            style={{
              fontWeight: "normal",
              color: "#9a8c7c",
              fontSize: "13px",
            }}
          >
            {" "}
            via {item.source}
          </span>
        )}
      </div>
    </article>
  );
}

export function TestimonialsRustic({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "var(--color-cream, #faf6f0)",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "1000px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: "12px",
            fontFamily: "Georgia, serif",
          }}
        >
          {title ?? "What Our Clients Say"}
        </h2>
        <p
          style={{
            textAlign: "center",
            color: "#9a8c7c",
            fontSize: "11px",
            marginBottom: "50px",
            fontFamily: "sans-serif",
            maxWidth: "600px",
            margin: "0 auto 50px",
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
            <TestimonialsRusticCard key={item.reviewer} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
