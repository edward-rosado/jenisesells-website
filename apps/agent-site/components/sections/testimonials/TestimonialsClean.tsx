"use client";

import { useState } from "react";
import { clampRating, FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";
import type { TestimonialItem } from "@/lib/types";

function TestimonialsCleanCard({ item }: { item: TestimonialItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "white",
        borderRadius: "8px",
        padding: "24px",
        border: "1px solid #eee",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      <span
        role="img"
        aria-label={`${clampRating(item.rating)} out of 5 stars`}
        style={{
          display: "block",
          color: "var(--color-accent)",
          fontSize: "16px",
          marginBottom: "12px",
        }}
      >
        {"★".repeat(clampRating(item.rating))}{"☆".repeat(5 - clampRating(item.rating))}
      </span>
      <p style={{
        color: "#555",
        fontSize: "14px",
        lineHeight: 1.7,
        fontStyle: "italic",
      }}>
        {item.text}
      </p>
      <div style={{
        marginTop: "16px",
        fontWeight: 600,
        color: "#1a1a1a",
        fontSize: "14px",
      }}>
        — {item.reviewer}
        {item.source && (
          <span style={{ fontWeight: "normal", color: "#767676" }}>
            {" "}via {item.source}
          </span>
        )}
      </div>
    </article>
  );
}

export function TestimonialsClean({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#fafafa",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 600,
          color: "#1a1a1a",
          marginBottom: "8px",
          letterSpacing: "-0.3px",
        }}>
          {title ?? "What My Clients Say"}
        </h2>
        <p style={{
          textAlign: "center",
          color: "#767676",
          fontSize: "12px",
          marginBottom: "50px",
        }}>
          {FTC_DISCLAIMER}
        </p>
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
          gap: "24px",
        }}>
          {items.map((item) => (
            <TestimonialsCleanCard key={item.reviewer} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
