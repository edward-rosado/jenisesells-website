"use client";

import { useState } from "react";
import { useReducedMotion } from "@/hooks/useReducedMotion";
import type { MarqueeProps } from "@/components/sections/types";

export function MarqueeBanner({ items, title }: MarqueeProps) {
  const reducedMotion = useReducedMotion();
  const [hovered, setHovered] = useState(false);

  if (items.length === 0) return null;

  const isStatic = reducedMotion || items.length === 1;
  const displayItems = isStatic ? items : [...items, ...items];
  const duration = items.length * 3;

  const renderItem = (item: (typeof items)[number], index: number) => {
    const content = (
      <span
        data-marquee-item
        key={index}
        style={{
          color: "rgba(0,0,0,0.35)",
          fontSize: "14px",
          fontWeight: 600,
          letterSpacing: "3px",
          textTransform: "uppercase" as const,
          whiteSpace: "nowrap" as const,
        }}
      >
        {item.text}
      </span>
    );

    if (item.link) {
      return (
        <a
          key={index}
          href={item.link}
          tabIndex={-1}
          aria-hidden="true"
          style={{ textDecoration: "none" }}
        >
          {content}
        </a>
      );
    }

    return content;
  };

  const separator = (key: string) => (
    <span key={key} style={{ color: "rgba(0,0,0,0.15)", fontSize: "8px", margin: "0 24px" }}>
      ◆
    </span>
  );

  const interleaved: React.ReactNode[] = [];
  displayItems.forEach((item, i) => {
    if (i > 0) interleaved.push(separator(`sep-${i}`));
    interleaved.push(renderItem(item, i));
  });

  return (
    <div
      aria-hidden="true"
      style={{
        background: "var(--color-bg, #fafaf8)",
        padding: "20px 0",
        overflow: "hidden",
        position: "relative" as const,
      }}
    >
      {!isStatic && (
        <style>{`
          @keyframes marquee-scroll {
            0% { transform: translateX(0); }
            100% { transform: translateX(-50%); }
          }
        `}</style>
      )}
      {title && (
        <div style={{
          textAlign: "center" as const,
          fontSize: "11px",
          textTransform: "uppercase" as const,
          letterSpacing: "3px",
          color: "rgba(0,0,0,0.4)",
          marginBottom: "12px",
          fontWeight: 500,
        }}>
          {title}
        </div>
      )}
      <div
        data-marquee-track
        onMouseEnter={() => setHovered(true)}
        onMouseLeave={() => setHovered(false)}
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: isStatic ? "center" : undefined,
          gap: isStatic ? "24px" : undefined,
          ...(isStatic
            ? { flexWrap: "wrap" as const }
            : {
                animation: `marquee-scroll ${duration}s linear infinite`,
                animationPlayState: hovered ? "paused" : "running",
                width: "max-content",
              }),
        }}
      >
        {interleaved}
      </div>
    </div>
  );
}
