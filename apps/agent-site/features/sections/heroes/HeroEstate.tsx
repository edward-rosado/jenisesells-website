"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/features/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroEstate({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      id="hero"
      style={{
        background: "var(--color-bg, #faf6f0)",
        color: "white",
        position: "relative",
        minHeight: "520px",
        display: "flex",
        alignItems: "flex-end",
        justifyContent: "center",
        overflow: "hidden",
      }}
    >
      {/* Dark gradient overlay from bottom */}
      <div
        aria-hidden="true"
        style={{
          position: "absolute",
          inset: 0,
          background:
            "linear-gradient(to top, rgba(30,40,25,0.88) 0%, rgba(30,40,25,0.45) 55%, rgba(30,40,25,0.10) 100%)",
          zIndex: 1,
        }}
      />

      {/* Content */}
      <div
        style={{
          position: "relative",
          zIndex: 2,
          maxWidth: "900px",
          width: "100%",
          padding: "80px 40px 60px",
          display: "flex",
          gap: "48px",
          alignItems: "flex-end",
          flexWrap: "wrap",
        }}
      >
        <div style={{ flex: 1, minWidth: "280px" }}>
          {data.tagline && (
            <p
              style={{
                fontSize: "13px",
                letterSpacing: "2px",
                textTransform: "uppercase",
                color: "rgba(255,255,255,0.75)",
                marginBottom: "12px",
                fontFamily: "sans-serif",
              }}
            >
              {data.tagline}
            </p>
          )}

          <h1
            style={{
              fontSize: "clamp(32px, 5vw, 56px)",
              fontWeight: 700,
              lineHeight: 1.1,
              marginBottom: "20px",
              fontFamily: "Georgia, serif",
              color: "white",
            }}
          >
            {renderHeadline(data.headline, data.highlight_word)}
          </h1>

          {data.body && (
            <p
              style={{
                fontSize: "16px",
                color: "rgba(255,255,255,0.82)",
                marginBottom: "28px",
                lineHeight: 1.65,
                fontFamily: "sans-serif",
                maxWidth: "500px",
              }}
            >
              {data.body}
            </p>
          )}

          <a
            href={safeHref(data.cta_link)}
            onMouseEnter={() => setCtaHover(true)}
            onMouseLeave={() => setCtaHover(false)}
            onFocus={() => setCtaHover(true)}
            onBlur={() => setCtaHover(false)}
            style={{
              display: "inline-block",
              background: ctaHover
                ? "var(--color-accent, #5a7a51)"
                : "var(--color-accent, #4a6741)",
              color: "white",
              padding: "14px 32px",
              borderRadius: "4px",
              fontSize: "15px",
              fontWeight: 600,
              textDecoration: "none",
              fontFamily: "sans-serif",
              letterSpacing: "0.5px",
              transition: "all 0.3s",
              transform: ctaHover ? "translateY(-2px)" : "none",
            }}
          >
            {data.cta_text}
          </a>
        </div>

        {agentPhotoUrl && (
          <div
            style={{
              width: "320px",
              height: "400px",
              borderRadius: "6px",
              overflow: "hidden",
              flexShrink: 0,
              border: "3px solid rgba(255,255,255,0.3)",
            }}
          >
            <Image
              src={agentPhotoUrl}
              alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
              width={320}
              height={400}
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
              priority
            />
          </div>
        )}
      </div>
    </section>
  );
}
