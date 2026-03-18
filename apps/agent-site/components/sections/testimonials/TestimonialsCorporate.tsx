"use client";

import { useState } from "react";
import { FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";
import type { TestimonialItem } from "@/lib/types";

function TestimonialsCorporateCard({ item }: { item: TestimonialItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#f8fafc",
        border: "1px solid #e2e8f0",
        borderRadius: "6px",
        padding: "28px",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      {/* Quote mark decorative element */}
      <div
        aria-hidden="true"
        style={{
          fontSize: "32px",
          color: "#2563eb",
          lineHeight: 1,
          marginBottom: "12px",
          fontFamily: "Georgia, serif",
          opacity: 0.4,
        }}
      >
        &ldquo;
      </div>

      <p
        style={{
          color: "#334155",
          fontSize: "15px",
          lineHeight: 1.7,
          fontStyle: "italic",
          marginBottom: "20px",
        }}
      >
        {item.text}
      </p>

      <div
        style={{
          fontWeight: 700,
          color: "#0f172a",
          fontSize: "14px",
        }}
      >
        — {item.reviewer}
        {item.source && (
          <span
            style={{
              fontWeight: 400,
              color: "#64748b",
            }}
          >
            , {item.source}
          </span>
        )}
      </div>
    </article>
  );
}

export function TestimonialsCorporate({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "white",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "#0f172a",
            marginBottom: "8px",
            letterSpacing: "-0.3px",
          }}
        >
          {title ?? "Client Testimonials"}
        </h2>
        <p
          style={{
            textAlign: "center",
            color: "#94a3b8",
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
            <TestimonialsCorporateCard key={item.reviewer} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
