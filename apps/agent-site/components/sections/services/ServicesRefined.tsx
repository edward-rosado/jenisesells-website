"use client";

import { useState } from "react";
import type { ServicesProps } from "@/components/sections/types";
import type { ServiceItem } from "@/features/config/types";

function ServicesRefinedCard({ item, index }: { item: ServiceItem; index: number }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: index % 2 === 0 ? "#ffffff" : "#f8f6f3",
        borderLeft: "2px solid var(--color-accent, #b8926a)",
        padding: "28px 40px",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      <h3
        style={{
          fontSize: "20px",
          fontWeight: 400,
          color: "var(--color-primary, #3d3028)",
          marginBottom: "8px",
          fontFamily: "var(--font-family, Georgia), serif",
        }}
      >
        {item.title}
      </h3>
      <p
        style={{
          fontSize: "15px",
          color: "var(--color-secondary, #5a4a3a)",
          lineHeight: 1.7,
          margin: 0,
        }}
      >
        {item.description}
      </p>
    </article>
  );
}

export function ServicesRefined({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="features"
      style={{
        padding: "80px 0",
      }}
    >
      <div style={{ maxWidth: "800px", margin: "0 auto", paddingLeft: "40px", paddingRight: "40px" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 300,
            color: "var(--color-primary, #3d3028)",
            marginBottom: subtitle ? "10px" : "50px",
            fontFamily: "var(--font-family, Georgia), serif",
            letterSpacing: "1px",
          }}
        >
          {title ?? "Our Services"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "var(--color-secondary, #5a4a3a)",
              fontSize: "16px",
              marginBottom: "50px",
            }}
          >
            {subtitle}
          </p>
        )}
      </div>
      <div style={{ maxWidth: "800px", margin: "0 auto" }}>
        {items.map((item, index) => (
          <ServicesRefinedCard key={item.title} item={item} index={index} />
        ))}
      </div>
    </section>
  );
}
