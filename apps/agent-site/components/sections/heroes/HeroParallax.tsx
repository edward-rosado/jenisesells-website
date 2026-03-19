"use client";

import { useRef } from "react";
import { useParallax } from "@/hooks/useParallax";
import type { HeroProps } from "@/components/sections/types";

export function HeroParallax({ data, agentPhotoUrl, agentName }: HeroProps) {
  const heroRef = useRef<HTMLDivElement>(null);
  const bgRef = useRef<HTMLDivElement>(null);

  useParallax(heroRef, bgRef);

  const bgImage = data.background_image ?? agentPhotoUrl;

  // Split headline by highlight_word
  let headlineParts: React.ReactNode = data.headline;
  if (data.highlight_word && data.headline.includes(data.highlight_word)) {
    const idx = data.headline.indexOf(data.highlight_word);
    headlineParts = (
      <>
        {data.headline.slice(0, idx)}
        <span style={{ color: "var(--color-accent, #81C784)" }}>{data.highlight_word}</span>
        {data.headline.slice(idx + data.highlight_word.length)}
      </>
    );
  }

  return (
    <div
      ref={heroRef}
      data-hero-parallax
      style={{
        position: "relative" as const,
        height: "100vh",
        overflow: "hidden",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      {/* Parallax background */}
      <div
        ref={bgRef}
        data-parallax-bg
        style={{
          position: "absolute" as const,
          inset: "-10%",
          backgroundImage: bgImage ? `url('${bgImage}')` : undefined,
          backgroundColor: bgImage ? undefined : "var(--color-primary, #1a1a2e)",
          backgroundSize: "cover",
          backgroundPosition: "center",
          willChange: "transform",
        }}
      />

      {/* Dark overlay */}
      <div
        data-overlay
        style={{
          position: "absolute" as const,
          inset: "0",
          background: "rgba(0,0,0,0.45)",
        }}
      />

      {/* Content */}
      <div style={{
        position: "relative" as const,
        zIndex: 2,
        textAlign: "center" as const,
        color: "white",
        maxWidth: "700px",
        padding: "0 24px",
      }}>
        {agentName && (
          <div style={{
            fontSize: "12px",
            textTransform: "uppercase" as const,
            letterSpacing: "4px",
            color: "rgba(255,255,255,0.7)",
            marginBottom: "16px",
          }}>
            {agentName}
          </div>
        )}

        {data.tagline && (
          <div style={{
            fontSize: "14px",
            textTransform: "uppercase" as const,
            letterSpacing: "3px",
            color: "rgba(255,255,255,0.6)",
            marginBottom: "12px",
          }}>
            {data.tagline}
          </div>
        )}

        <h1 style={{
          fontSize: "56px",
          fontWeight: 700,
          lineHeight: 1.1,
          marginBottom: "20px",
          fontFamily: "var(--font-family, inherit)",
        }}>
          {headlineParts}
        </h1>

        {data.body && (
          <p style={{
            fontSize: "18px",
            lineHeight: 1.7,
            color: "rgba(255,255,255,0.85)",
            marginBottom: "32px",
          }}>
            {data.body}
          </p>
        )}

        <a
          href={data.cta_link}
          style={{
            display: "inline-block",
            background: "var(--color-accent, #81C784)",
            color: "var(--color-primary, #1B5E20)",
            padding: "16px 36px",
            borderRadius: "8px",
            fontWeight: 700,
            fontSize: "16px",
            textDecoration: "none",
          }}
        >
          {data.cta_text}
        </a>
      </div>

      {/* Responsive + scroll indicator */}
      <style>{`
        @media (max-width: 768px) {
          [data-hero-parallax] h1 { font-size: 36px !important; }
          [data-hero-parallax] p { font-size: 16px !important; }
        }
        @keyframes hero-bounce {
          0%, 100% { transform: translateX(-50%) translateY(0); }
          50% { transform: translateX(-50%) translateY(8px); }
        }
      `}</style>
      <div
        data-scroll-indicator
        style={{
          position: "absolute" as const,
          bottom: "32px",
          left: "50%",
          transform: "translateX(-50%)",
          zIndex: 2,
          color: "rgba(255,255,255,0.5)",
          fontSize: "12px",
          letterSpacing: "2px",
          textTransform: "uppercase" as const,
          animation: "hero-bounce 2s infinite",
        }}
      >
        ↓
      </div>
    </div>
  );
}
