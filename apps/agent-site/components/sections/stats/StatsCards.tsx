"use client";

import { useState } from "react";
import type { StatsProps } from "@/components/sections/types";
import type { StatItem } from "@/lib/types";

function StatsCardItem({ item }: { item: StatItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        border: "1px solid #eee",
        borderRadius: "12px",
        padding: "24px 32px",
        textAlign: "center",
        minWidth: "140px",
        display: "flex",
        flexDirection: "column",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      <dt style={{
        fontSize: "12px",
        textTransform: "uppercase",
        letterSpacing: "1px",
        marginTop: "4px",
        color: "#555",
        order: 2,
      }}>
        {item.label}
      </dt>
      <dd style={{
        fontSize: "28px",
        fontWeight: 700,
        color: "#1a1a1a",
        margin: 0,
        order: 1,
      }}>
        {item.value}
      </dd>
    </div>
  );
}

export function StatsCards({ items, sourceDisclaimer }: StatsProps) {
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
        maxWidth: "800px",
        margin: "0 auto",
      }}>
        {items.map((item) => (
          <StatsCardItem key={item.label} item={item} />
        ))}
      </dl>
      {sourceDisclaimer && (
        <p style={{
          textAlign: "center",
          color: "#767676",
          fontSize: "11px",
          marginTop: "16px",
        }}>
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
