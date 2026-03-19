"use client";

import { useReducedMotion } from "@/hooks/useReducedMotion";
import type { MarqueeProps } from "@/components/sections/types";

export function MarqueeBanner({ items, title }: MarqueeProps) {
  const reducedMotion = useReducedMotion();

  if (items.length === 0) return null;

  const isStatic = reducedMotion || items.length === 1;
  const duration = Math.max(20, items.length * 8);

  const itemStyle = {
    color: "rgba(0,0,0,0.35)",
    fontSize: "14px",
    fontWeight: 600,
    letterSpacing: "3px",
    textTransform: "uppercase" as const,
    whiteSpace: "nowrap" as const,
  };

  const separatorStyle = {
    color: "rgba(0,0,0,0.15)",
    fontSize: "8px",
    margin: "0 24px",
  };

  /* Build one complete set: item ◆ item ◆ item ◆ (trailing separator
     ensures the seam between clone A and clone B is identical spacing) */
  const buildSet = (keyPrefix: string) =>
    items.map((item, i) => {
      const sep = (
        <span key={`${keyPrefix}-sep-${i}`} style={separatorStyle}>◆</span>
      );
      const content = (
        <span data-marquee-item key={`${keyPrefix}-${i}`} style={itemStyle}>
          {item.text}
        </span>
      );
      const wrapped = item.link ? (
        <a
          key={`${keyPrefix}-${i}`}
          href={item.link}
          tabIndex={-1}
          aria-hidden="true"
          style={{ textDecoration: "none" }}
        >
          {content}
        </a>
      ) : content;

      // Every item gets a trailing separator (including last) so the
      // seam between set-a end and set-b start is seamless
      return <span key={`${keyPrefix}-g-${i}`} style={{ display: "contents" }}>{wrapped}{sep}</span>;
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
        style={{
          display: "flex",
          alignItems: "center",
          ...(isStatic
            ? { justifyContent: "center", gap: "24px", flexWrap: "wrap" as const }
            : {
                animation: `marquee-scroll ${duration}s linear infinite`,
                width: "max-content",
              }),
        }}
      >
        {isStatic
          ? items.map((item, i) => (
              <span data-marquee-item key={i} style={itemStyle}>{item.text}</span>
            ))
          : <>{buildSet("a")}{buildSet("b")}</>
        }
      </div>
    </div>
  );
}
