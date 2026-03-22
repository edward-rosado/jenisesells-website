"use client";

import { useState } from "react";
import type { ServicesProps } from "@/components/sections/types";
import type { ServiceItem } from "@/features/config/types";

function ServicesEstateCard({ item }: { item: ServiceItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#f5f0e8",
        borderRadius: "8px",
        padding: "24px 28px",
        display: "flex",
        gap: "24px",
        alignItems: "flex-start",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      {/* Icon placeholder area */}
      <div
        aria-hidden="true"
        style={{
          width: "44px",
          height: "44px",
          borderRadius: "50%",
          background: "var(--color-accent, #4a6741)",
          flexShrink: 0,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          color: "white",
          fontSize: "20px",
        }}
      >
        ◆
      </div>

      {/* Text content */}
      <div>
        <h3
          style={{
            color: "var(--color-primary, #2d4a3e)",
            fontSize: "18px",
            fontWeight: 700,
            marginBottom: "6px",
            fontFamily: "Georgia, serif",
          }}
        >
          {item.title}
        </h3>
        <p
          style={{
            color: "#5a5040",
            fontSize: "15px",
            lineHeight: 1.6,
            fontFamily: "sans-serif",
          }}
        >
          {item.description}
        </p>
      </div>
    </article>
  );
}

export function ServicesEstate({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="features"
      style={{
        background: "var(--color-bg, #faf6f0)",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "10px" : "48px",
            fontFamily: "Georgia, serif",
          }}
        >
          {title ?? "Our Services"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "var(--color-secondary, #8b6b3d)",
              fontSize: "16px",
              marginBottom: "48px",
              fontFamily: "sans-serif",
            }}
          >
            {subtitle}
          </p>
        )}

        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "20px",
          }}
        >
          {items.map((item) => (
            <ServicesEstateCard key={item.title} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
