"use client";

import { useState } from "react";
import type { ServicesProps } from "@/features/sections/types";
import type { ServiceItem } from "@/features/config/types";

function ServicesPillsCard({ item }: { item: ServiceItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "white",
        borderRadius: "16px",
        padding: "28px",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 12px rgba(0,0,0,0.07)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        position: "relative",
        overflow: "hidden",
      }}
    >
      {item.category && (
        <span
          data-category-pill
          style={{
            position: "absolute",
            top: "16px",
            right: "16px",
            background: "var(--color-accent, #ff6b6b)",
            color: "white",
            fontSize: "11px",
            fontWeight: 700,
            padding: "3px 10px",
            borderRadius: "12px",
            textTransform: "uppercase" as const,
            letterSpacing: "0.5px",
          }}
        >
          {item.category}
        </span>
      )}
      <h3
        style={{
          fontSize: "18px",
          fontWeight: 700,
          color: "var(--color-primary, #1a1a1a)",
          marginBottom: "10px",
          fontFamily: "var(--font-family, Inter), sans-serif",
        }}
      >
        {item.title}
      </h3>
      <p
        style={{
          fontSize: "15px",
          color: "#555",
          lineHeight: 1.6,
        }}
      >
        {item.description}
      </p>
    </article>
  );
}

export function ServicesPills({ items, title, subtitle }: ServicesProps) {
  return (
    <section
      id="features"
      style={{
        background: "#fafafa",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 800,
            color: "var(--color-primary, #1a1a1a)",
            marginBottom: "10px",
            fontFamily: "var(--font-family, Inter), sans-serif",
          }}
        >
          {title ?? "What I Offer"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#666",
              fontSize: "16px",
              marginBottom: "45px",
            }}
          >
            {subtitle}
          </p>
        )}
        {!subtitle && <div style={{ marginBottom: "45px" }} />}

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <ServicesPillsCard key={item.title} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
