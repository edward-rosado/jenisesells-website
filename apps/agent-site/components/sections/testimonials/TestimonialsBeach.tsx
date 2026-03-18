"use client";

import { useState } from "react";
import { clampRating, FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";

export function TestimonialsBeach({ items, title }: TestimonialsProps) {
  const [hovered, setHovered] = useState<number | null>(null);
  return (
    <section
      id="testimonials"
      style={{
        background: "var(--color-bg-alt, #f0f8fa)",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2c7a7b)",
            marginBottom: "8px",
          }}
        >
          {title ?? "What Our Clients Say"}
        </h2>
        <p
          style={{
            textAlign: "center",
            color: "#6a8a8a",
            fontSize: "12px",
            marginBottom: "45px",
          }}
        >
          {FTC_DISCLAIMER}
        </p>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
            gap: "0",
          }}
        >
          {items.map((item, index) => (
            <div key={item.reviewer}>
              <article
                onMouseEnter={() => setHovered(index)}
                onMouseLeave={() => setHovered(null)}
                style={{
                  background: "#fefcf8",
                  borderRadius: "12px",
                  padding: "28px",
                  boxShadow:
                    hovered === index
                      ? "0 8px 24px rgba(44, 122, 123, 0.18)"
                      : "0 2px 10px rgba(44, 122, 123, 0.07)",
                  transform: hovered === index ? "translateY(-4px)" : "translateY(0)",
                  transition: "transform 0.2s ease, box-shadow 0.2s ease",
                  cursor: "default",
                }}
              >
                <span
                  role="img"
                  aria-label={`${clampRating(item.rating)} out of 5 stars`}
                  style={{
                    display: "block",
                    color: "var(--color-primary, #2c7a7b)",
                    fontSize: "18px",
                    marginBottom: "12px",
                  }}
                >
                  {"★".repeat(clampRating(item.rating))}
                  {"☆".repeat(5 - clampRating(item.rating))}
                </span>
                <p
                  style={{
                    fontStyle: "italic",
                    color: "#3a5a5a",
                    fontSize: "14px",
                    lineHeight: 1.7,
                    marginBottom: "16px",
                  }}
                >
                  {item.text}
                </p>
                <div
                  style={{
                    fontWeight: 700,
                    color: "var(--color-primary, #2c7a7b)",
                    fontSize: "14px",
                  }}
                >
                  {item.reviewer}
                </div>
                {item.source && (
                  <div style={{ color: "#6a8a8a", fontSize: "12px" }}>via {item.source}</div>
                )}
              </article>
              {/* Dashed separator between cards (not after last) */}
              {index < items.length - 1 && (
                <hr
                  style={{
                    borderWidth: "1px 0 0 0",
                    borderStyle: "dashed",
                    borderColor: "var(--color-primary, #2c7a7b)",
                    opacity: 0.3,
                    margin: "16px 0",
                  }}
                />
              )}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
