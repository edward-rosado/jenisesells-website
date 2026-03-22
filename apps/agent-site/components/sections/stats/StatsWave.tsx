"use client";

import { useState } from "react";
import type { StatsProps } from "@/components/sections/types";
import type { StatItem } from "@/features/config/types";

function StatsWaveItem({ item }: { item: StatItem }) {
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
          fontSize: "13px",
          textTransform: "uppercase",
          letterSpacing: "1.5px",
          color: "rgba(255,255,255,0.85)",
          order: 2,
        }}
      >
        {item.label}
      </dt>
      <dd
        style={{
          fontSize: "34px",
          fontWeight: 800,
          color: "var(--color-accent, #e8f4f8)",
          margin: 0,
          marginBottom: "6px",
          order: 1,
        }}
      >
        {item.value}
      </dd>
    </div>
  );
}

export function StatsWave({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "var(--color-primary, #2c7a7b)",
        color: "white",
        padding: "50px 40px",
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
          <StatsWaveItem key={item.label} item={item} />
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.85)",
            fontSize: "11px",
            marginTop: "16px",
            marginBottom: 0,
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
