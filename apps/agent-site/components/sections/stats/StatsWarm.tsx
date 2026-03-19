"use client";

import { useState } from "react";
import type { StatsProps } from "@/components/sections/types";

function StatCard({ value, label }: { value: string; label: string }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "white",
        borderRadius: "16px",
        padding: "28px 36px",
        textAlign: "center",
        boxShadow: hover
          ? "0 8px 28px rgba(90,158,124,0.22)"
          : "0 4px 16px rgba(90,158,124,0.1)",
        minWidth: "160px",
        transition: "transform 0.3s, box-shadow 0.3s",
        transform: hover ? "translateY(-4px)" : "none",
        cursor: "default",
      }}
    >
      <dl style={{ margin: 0, display: "flex", flexDirection: "column" }}>
        <dt
          style={{
            fontSize: "13px",
            color: "#4a6b5a",
            marginTop: "6px",
            fontWeight: 600,
            letterSpacing: "0.5px",
            order: 2,
          }}
        >
          {label}
        </dt>
        <dd
          style={{
            fontSize: "36px",
            fontWeight: 700,
            color: "var(--color-accent, #5a9e7c)",
            margin: 0,
            fontFamily: "var(--font-family, Nunito), sans-serif",
            order: 1,
          }}
        >
          {value}
        </dd>
      </dl>
    </div>
  );
}

export function StatsWarm({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      style={{
        background: "#f0f7f4",
        padding: "50px 40px",
      }}
    >
      <div
        style={{
          maxWidth: "1000px",
          margin: "0 auto",
          display: "flex",
          flexWrap: "wrap",
          justifyContent: "center",
          gap: "20px",
        }}
      >
        {items.map((item) => (
          <StatCard key={item.label} value={item.value} label={item.label} />
        ))}
      </div>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "#7a9a88",
            fontSize: "11px",
            marginTop: "20px",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
