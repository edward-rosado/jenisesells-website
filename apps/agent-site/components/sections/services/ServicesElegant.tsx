"use client";

import { useState } from "react";
import type { ServicesProps } from "@/components/sections/types";
import type { ServiceItem } from "@/lib/types";

function ServicesElegantCard({ item }: { item: ServiceItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        borderLeft: `3px solid var(--color-accent, #d4af37)`,
        paddingLeft: "24px",
        paddingTop: "8px",
        paddingBottom: "8px",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        borderRadius: "0 8px 8px 0",
      }}
    >
      <h3
        style={{
          fontSize: "20px",
          fontWeight: 400,
          color: "white",
          marginBottom: "8px",
          fontFamily: "var(--font-family, Georgia), serif",
        }}
      >
        {item.title}
      </h3>
      <p
        style={{
          fontSize: "15px",
          color: "rgba(255,255,255,0.65)",
          lineHeight: 1.7,
          margin: 0,
        }}
      >
        {item.description}
      </p>
    </article>
  );
}

export function ServicesElegant({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="features"
      style={{
        background: "var(--color-primary, #0a0a0a)",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 300,
            color: "white",
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
              color: "rgba(255,255,255,0.6)",
              fontSize: "16px",
              marginBottom: "50px",
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <ServicesElegantCard key={item.title} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
