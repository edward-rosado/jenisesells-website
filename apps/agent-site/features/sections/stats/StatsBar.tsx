"use client";

import { useState } from "react";
import type { StatsProps } from "@/features/sections/types";
import type { StatItem } from "@/features/config/types";

function StatsBarItem({ item }: { item: StatItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      key={item.label}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        textAlign: "center",
        color: "white",
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
          letterSpacing: "1px",
          marginTop: "4px",
          order: 2,
        }}
      >
        {item.label}
      </dt>
      <dd
        style={{
          fontSize: "32px",
          fontWeight: 800,
          color: "var(--color-accent)",
          margin: 0,
        }}
      >
        {item.value}
      </dd>
    </div>
  );
}

export function StatsBar({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "var(--color-primary)",
        padding: "30px 40px",
      }}
    >
      <dl
        style={{
          display: "flex",
          justifyContent: "center",
          gap: "50px",
          flexWrap: "wrap",
          margin: 0,
        }}
      >
        {items.map((item) => (
          <StatsBarItem key={item.label} item={item} />
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.85)",
            fontSize: "12px",
            marginTop: "12px",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
