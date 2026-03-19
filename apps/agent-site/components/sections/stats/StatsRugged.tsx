"use client";

import { useState } from "react";
import type { StatsProps } from "@/components/sections/types";
import type { StatItem } from "@/lib/types";

function StatsRuggedItem({ item }: { item: StatItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        textAlign: "center",
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
          fontSize: "12px",
          textTransform: "uppercase",
          letterSpacing: "1.5px",
          color: "rgba(255,255,255,0.85)",
          marginTop: "6px",
          fontFamily: "sans-serif",
          order: 2,
        }}
      >
        {item.label}
      </dt>
      <dd
        style={{
          fontSize: "36px",
          fontWeight: 800,
          color: "var(--color-accent, #c8a84b)",
          margin: 0,
          fontFamily: "Georgia, serif",
          order: 1,
        }}
      >
        {item.value}
      </dd>
    </div>
  );
}

export function StatsRugged({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "var(--color-primary, #2d4a3e)",
        padding: "40px 40px",
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
          <StatsRuggedItem key={item.label} item={item} />
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.85)",
            fontSize: "11px",
            marginTop: "14px",
            fontFamily: "sans-serif",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
