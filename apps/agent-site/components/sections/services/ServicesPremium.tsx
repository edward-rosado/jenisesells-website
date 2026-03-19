"use client";

import { useRef } from "react";
import Image from "next/image";
import { useScrollReveal } from "@/hooks/useScrollReveal";
import type { FeaturesProps } from "@/components/sections/types";

const LIGHT_BG = ["#faf7f2", "#f0f7ff", "#faf7f2"];
const DARK_BG = "#1a1a2e";

export function ServicesPremium({ items, title, subtitle }: FeaturesProps) {
  if (items.length === 0) return null;

  return (
    <section id="features" style={{ overflow: "hidden" }}>
      <style>{`
        @media (max-width: 768px) {
          [data-feature-block] {
            flex-direction: column !important;
            padding: 48px 24px !important;
            min-height: auto !important;
            gap: 32px !important;
          }
          [data-feature-block] h3 { font-size: 28px !important; }
          [data-feature-block] .visual-shape { width: 240px !important; height: 240px !important; }
          [data-feature-visual] {
            width: 100% !important;
            max-width: 100% !important;
            justify-content: center !important;
          }
          [data-feature-image] {
            max-width: 100% !important;
          }
        }
      `}</style>
      {(title || subtitle) && (
        <div style={{ textAlign: "center" as const, padding: "60px 24px 0" }}>
          {title && (
            <h2 style={{
              fontSize: "32px",
              fontWeight: 700,
              color: "var(--color-text, #1a1a1a)",
              fontFamily: "var(--font-family, inherit)",
              marginBottom: subtitle ? "12px" : "0",
            }}>
              {title}
            </h2>
          )}
          {subtitle && (
            <p style={{ fontSize: "16px", color: "#666", maxWidth: "600px", margin: "0 auto" }}>
              {subtitle}
            </p>
          )}
        </div>
      )}
      {items.map((item, i) => (
        <FeatureBlock key={i} item={item} index={i} />
      ))}
    </section>
  );
}

interface FeatureBlockProps {
  item: FeaturesProps["items"][number];
  index: number;
}

function FeatureBlock({ item, index }: FeatureBlockProps) {
  const ref = useRef<HTMLDivElement>(null);
  const isVisible = useScrollReveal(ref);
  const isDark = index % 2 === 1;
  const isReversed = index % 2 === 1;
  const bg = item.background_color ?? (isDark ? DARK_BG : LIGHT_BG[index % LIGHT_BG.length]);

  return (
    <div
      ref={ref}
      data-feature-block
      data-direction={isReversed ? "reversed" : "normal"}
      style={{
        width: "100%",
        minHeight: "70vh",
        display: "flex",
        alignItems: "center",
        padding: "80px 60px",
        gap: "60px",
        flexDirection: isReversed ? "row-reverse" : "row",
        background: bg,
        overflow: "hidden",
      }}
    >
      <div style={{
        maxWidth: "560px",
        opacity: isVisible ? 1 : 0,
        transform: isVisible ? "translateY(0)" : "translateY(40px)",
        transition: "opacity 0.8s ease, transform 0.8s ease",
      }}>
        {item.category && (
          <div style={{
            fontSize: "12px",
            textTransform: "uppercase" as const,
            letterSpacing: "3px",
            marginBottom: "12px",
            fontWeight: 600,
            color: isDark ? "#90caf9" : "var(--color-primary, #81C784)",
          }}>
            {item.category}
          </div>
        )}
        <h3 style={{
          fontSize: "36px",
          fontWeight: 700,
          marginBottom: "16px",
          lineHeight: 1.2,
          color: isDark ? "#fff" : "var(--color-primary, #1B5E20)",
          fontFamily: "var(--font-family, inherit)",
        }}>
          {item.title}
        </h3>
        <p style={{
          fontSize: "17px",
          lineHeight: 1.7,
          color: isDark ? "rgba(255,255,255,0.65)" : "#555",
        }}>
          {item.description}
        </p>
      </div>

      <div
        data-feature-visual
        style={{
          flex: 1,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          opacity: isVisible ? 1 : 0,
          transform: isVisible ? "scale(1)" : "scale(0.92)",
          transition: "opacity 1s ease 0.2s, transform 1s ease 0.2s",
        }}
      >
        {item.image_url ? (
          <div
            data-feature-image
            style={{
              position: "relative" as const,
              width: "100%",
              maxWidth: "480px",
              aspectRatio: "4/3",
              borderRadius: "16px",
              overflow: "hidden",
              boxShadow: isDark ? "0 8px 32px rgba(0,0,0,0.4)" : "0 8px 32px rgba(0,0,0,0.1)",
            }}
          >
            <Image
              src={item.image_url}
              alt={item.title}
              fill
              sizes="(max-width: 768px) 100vw, 480px"
              style={{ objectFit: "cover" }}
            />
          </div>
        ) : (
          <div
            className="visual-shape"
            style={{
              width: "320px",
              height: "320px",
              borderRadius: "24px",
              background: isDark
                ? "linear-gradient(135deg, rgba(255,255,255,0.06), rgba(255,255,255,0.02))"
                : `linear-gradient(135deg, ${bg === "#f0f7ff" ? "#e3f2fd, #bbdefb" : "#e8f5e9, #c8e6c9"})`,
              border: isDark ? "1px solid rgba(255,255,255,0.08)" : undefined,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: item.icon ? "72px" : "80px",
            }}
          >
            {item.icon ?? ""}
          </div>
        )}
      </div>
    </div>
  );
}
