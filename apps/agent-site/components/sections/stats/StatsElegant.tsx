"use client";

import { useState } from "react";
import type { StatsProps } from "@/components/sections/types";
import type { StatItem } from "@/lib/types";

function StatsElegantItem({ item, isLast }: { item: StatItem; isLast: boolean }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
      }}
    >
      <div
        onMouseEnter={() => setHover(true)}
        onMouseLeave={() => setHover(false)}
        style={{
          textAlign: "center",
          padding: "0 48px",
          display: "flex",
          flexDirection: "column",
          boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
          transform: hover ? "translateY(-4px)" : "none",
          transition: "transform 0.3s, box-shadow 0.3s",
          cursor: "default",
          borderRadius: "8px",
        }}
      >
        <dt
          style={{
            fontSize: "11px",
            textTransform: "uppercase" as const,
            letterSpacing: "2px",
            marginTop: "6px",
            color: "var(--color-secondary, #5a4a3a)",
            order: 2,
          }}
        >
          {item.label}
        </dt>
        <dd
          style={{
            fontSize: "36px",
            fontWeight: 400,
            color: "var(--color-accent, #b8926a)",
            margin: 0,
            fontFamily: "var(--font-family, Georgia), serif",
            lineHeight: 1.2,
            order: 1,
          }}
        >
          {item.value}
        </dd>
      </div>
      {!isLast && (
        <div
          data-separator
          style={{
            width: "1px",
            height: "48px",
            background: "rgba(184,146,106,0.3)",
            flexShrink: 0,
          }}
        />
      )}
    </div>
  );
}

export function StatsElegant({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "#f8f6f3",
        padding: "50px 40px",
      }}
    >
      <dl
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "wrap",
          margin: 0,
          gap: 0,
        }}
      >
        {items.map((item, index) => (
          <StatsElegantItem key={item.label} item={item} isLast={index === items.length - 1} />
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "#8B7355",
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
