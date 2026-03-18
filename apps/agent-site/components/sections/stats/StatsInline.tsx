"use client";

import { useState } from "react";
import type { StatsProps } from "@/components/sections/types";

function StatCardInline({ item }: { item: { value: string; label: string } }) {
  const [hover, setHover] = useState(false);

  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#FFF8F0",
        borderRadius: "16px",
        padding: "24px 32px",
        textAlign: "center",
        minWidth: "140px",
        boxShadow: hover
          ? "0 6px 20px rgba(0,0,0,0.12)"
          : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      <dd style={{
        fontSize: "28px",
        fontWeight: 700,
        color: "#4A3728",
        margin: 0,
      }}>
        {item.value}
      </dd>
      <dt style={{
        fontSize: "12px",
        textTransform: "uppercase",
        letterSpacing: "1px",
        marginTop: "4px",
        color: "#8B7355",
      }}>
        {item.label}
      </dt>
    </div>
  );
}

export function StatsInline({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        padding: "50px 40px",
        background: "white",
      }}
    >
      <dl style={{
        display: "flex",
        justifyContent: "center",
        gap: "20px",
        flexWrap: "wrap",
        maxWidth: "900px",
        margin: "0 auto",
      }}>
        {items.map((item) => (
          <StatCardInline key={item.label} item={item} />
        ))}
      </dl>
      {sourceDisclaimer && (
        <p style={{
          textAlign: "center",
          color: "#B0A090",
          fontSize: "11px",
          marginTop: "16px",
        }}>
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
