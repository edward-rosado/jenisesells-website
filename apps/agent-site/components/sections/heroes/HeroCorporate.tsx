"use client";

import { useState } from "react";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroCorporate({ data }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      style={{
        background: "#f4f5f7",
        color: "#1e293b",
        paddingTop: "0",
        paddingBottom: "0",
        minHeight: "440px",
        position: "relative",
        display: "flex",
        flexDirection: "column",
      }}
    >
      {/* Blue gradient accent bar at top */}
      <div
        data-testid="accent-bar"
        style={{
          height: "4px",
          background: "linear-gradient(90deg, #2563eb 0%, #1d4ed8 100%)",
          width: "100%",
          flexShrink: 0,
        }}
      />

      <div
        style={{
          flex: 1,
          maxWidth: "1100px",
          margin: "0 auto",
          padding: "80px 40px 80px",
          width: "100%",
        }}
      >
        <h1
          style={{
            fontSize: "48px",
            fontWeight: 800,
            lineHeight: 1.1,
            marginBottom: "20px",
            color: "#0f172a",
            maxWidth: "700px",
            letterSpacing: "-0.5px",
          }}
        >
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>

        {data.tagline && (
          <p
            style={{
              fontSize: "20px",
              fontWeight: 600,
              color: "#2563eb",
              marginBottom: "16px",
            }}
          >
            {data.tagline}
          </p>
        )}

        {data.body && (
          <p
            style={{
              fontSize: "17px",
              color: "#475569",
              marginBottom: "36px",
              maxWidth: "560px",
              lineHeight: 1.7,
            }}
          >
            {data.body}
          </p>
        )}

        <div style={{ display: "flex", gap: "16px", flexWrap: "wrap" }}>
          {/* Primary CTA — blue filled */}
          <a
            href={safeHref(data.cta_link)}
            onMouseEnter={() => setCtaHover(true)}
            onMouseLeave={() => setCtaHover(false)}
            onFocus={() => setCtaHover(true)}
            onBlur={() => setCtaHover(false)}
            style={{
              display: "inline-block",
              background: ctaHover ? "#1d4ed8" : "#2563eb",
              color: "white",
              padding: "14px 32px",
              borderRadius: "4px",
              fontSize: "15px",
              fontWeight: 700,
              textDecoration: "none",
              transition: "background 0.2s",
              letterSpacing: "0.3px",
            }}
          >
            {data.cta_text}
          </a>

          {/* Secondary CTA — outlined */}
          <a
            href="#about"
            style={{
              display: "inline-block",
              background: "transparent",
              color: "#2563eb",
              padding: "14px 32px",
              borderRadius: "4px",
              fontSize: "15px",
              fontWeight: 600,
              textDecoration: "none",
              border: "2px solid #2563eb",
              transition: "background 0.2s, color 0.2s",
            }}
          >
            Learn More
          </a>
        </div>
      </div>
    </section>
  );
}
