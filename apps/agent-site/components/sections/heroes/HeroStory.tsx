"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroStory({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      id="hero"
      style={{
        position: "relative",
        minHeight: "520px",
        display: "flex",
        alignItems: "center",
        overflow: "hidden",
        background: "#f0f7f4",
      }}
    >
      {/* Full-width banner image */}
      {agentPhotoUrl && (
        <Image
          src={agentPhotoUrl}
          alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
          fill
          style={{ objectFit: "cover", objectPosition: "center top" }}
          sizes="100vw"
          priority
        />
      )}

      {/* Dark overlay for text readability */}
      <div
        aria-hidden="true"
        style={{
          position: "absolute",
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: agentPhotoUrl
            ? "linear-gradient(to right, rgba(45,74,62,0.85) 0%, rgba(45,74,62,0.6) 50%, rgba(45,74,62,0.3) 100%)"
            : "linear-gradient(135deg, rgba(90,158,124,0.08) 0%, rgba(240,247,244,0) 60%)",
          pointerEvents: "none",
        }}
      />

      {/* Content */}
      <div
        style={{
          maxWidth: "1200px",
          margin: "0 auto",
          width: "100%",
          padding: "100px 40px 80px",
          position: "relative",
          zIndex: 1,
        }}
      >
        <div style={{ maxWidth: "600px" }}>
          <h1
            style={{
              fontSize: "48px",
              fontWeight: 600,
              lineHeight: 1.2,
              marginBottom: "16px",
              color: agentPhotoUrl ? "#ffffff" : "var(--color-primary, #2d4a3e)",
              fontFamily: "var(--font-family, Nunito), sans-serif",
            }}
          >
            {renderHeadline(data.headline, data.highlight_word)}
          </h1>

          <p
            style={{
              fontSize: "22px",
              color: agentPhotoUrl
                ? "rgba(255,255,255,0.9)"
                : "var(--color-accent, #5a9e7c)",
              marginBottom: "16px",
              fontWeight: 600,
              fontFamily: "var(--font-family, Nunito), sans-serif",
            }}
          >
            {data.tagline}
          </p>

          {data.body && (
            <p
              style={{
                fontSize: "16px",
                color: agentPhotoUrl ? "rgba(255,255,255,0.85)" : "#4a6b5a",
                marginBottom: "32px",
                maxWidth: "520px",
                lineHeight: 1.7,
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
                ? "var(--color-primary, #2d4a3e)"
                : "var(--color-accent, #5a9e7c)",
              color: "white",
              padding: "14px 36px",
              borderRadius: "30px",
              fontSize: "16px",
              fontWeight: 600,
              textDecoration: "none",
              transition: "all 0.3s",
              transform: ctaHover ? "translateY(-2px)" : "none",
              boxShadow: ctaHover
                ? "0 6px 20px rgba(90,158,124,0.4)"
                : "0 2px 8px rgba(90,158,124,0.2)",
              fontFamily: "var(--font-family, Nunito), sans-serif",
            }}
          >
            {data.cta_text} <span aria-hidden="true">&rarr;</span>
          </a>
        </div>
      </div>
    </section>
  );
}
