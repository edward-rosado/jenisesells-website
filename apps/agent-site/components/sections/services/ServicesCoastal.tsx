"use client";

import { useState } from "react";
import type { ServicesProps } from "@/components/sections/types";

export function ServicesCoastal({ items, title, subtitle }: ServicesProps) {
  const [hovered, setHovered] = useState<number | null>(null);
  return (
    <section
      id="services"
      style={{
        padding: "70px 40px",
        background: "var(--color-bg, #e8f4f8)",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2c7a7b)",
            marginBottom: subtitle ? "8px" : "40px",
          }}
        >
          {title ?? "Our Services"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#4a6c6c",
              fontSize: "16px",
              marginBottom: "40px",
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "24px",
          }}
        >
          {items.map((item, index) => (
            <article
              key={item.title}
              onMouseEnter={() => setHovered(index)}
              onMouseLeave={() => setHovered(null)}
              style={{
                background: "#fefcf8",
                borderRadius: "12px",
                padding: "28px",
                borderTop: "3px solid var(--color-accent, #2c7a7b)",
                boxShadow:
                  hovered === index
                    ? "0 8px 24px rgba(44, 122, 123, 0.18)"
                    : "0 2px 10px rgba(44, 122, 123, 0.08)",
                transform: hovered === index ? "translateY(-4px)" : "translateY(0)",
                transition: "transform 0.2s ease, box-shadow 0.2s ease",
                cursor: "default",
              }}
            >
              <h3
                style={{
                  fontSize: "18px",
                  fontWeight: 700,
                  color: "var(--color-primary, #2c7a7b)",
                  marginBottom: "10px",
                }}
              >
                {item.title}
              </h3>
              <p
                style={{
                  fontSize: "15px",
                  color: "#4a6c6c",
                  lineHeight: 1.6,
                  margin: 0,
                }}
              >
                {item.description}
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
