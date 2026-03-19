"use client";

import { useState } from "react";
import type { StatsProps } from "@/components/sections/types";
import type { StatItem } from "@/lib/types";

function StatsOverlayItem({ item }: { item: StatItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        textAlign: "center",
        color: "white",
        display: "flex",
        flexDirection: "column",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        padding: "12px 16px",
        borderRadius: "8px",
      }}
    >
      <dt
        style={{
          fontSize: "11px",
          textTransform: "uppercase",
          letterSpacing: "2px",
          marginTop: "6px",
          color: "rgba(255,255,255,0.85)",
          order: 2,
        }}
      >
        {item.label}
      </dt>
      <dd
        style={{
          fontSize: "36px",
          fontWeight: 700,
          color: "var(--color-accent, #d4af37)",
          margin: 0,
          fontFamily: "var(--font-family, Georgia), serif",
          order: 1,
        }}
      >
        {item.value}
      </dd>
    </div>
  );
}

export function StatsOverlay({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "rgba(0,0,0,0.85)",
        padding: "40px",
      }}
    >
      <dl
        style={{
          display: "flex",
          justifyContent: "center",
          gap: "60px",
          flexWrap: "wrap",
          margin: 0,
        }}
      >
        {items.map((item) => (
          <StatsOverlayItem key={item.label} item={item} />
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.85)",
            fontSize: "11px",
            marginTop: "16px",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
