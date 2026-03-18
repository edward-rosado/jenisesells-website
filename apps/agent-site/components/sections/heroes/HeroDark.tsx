"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroDark({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      id="hero"
      style={{
        background: "linear-gradient(135deg, #0a0a0a 0%, var(--color-primary, #0a0a0a) 50%, var(--color-secondary, #1a1a2e) 100%)",
        color: "white",
        paddingTop: "90px",
        paddingBottom: "70px",
        paddingLeft: "40px",
        paddingRight: "40px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "50px",
        flexWrap: "wrap",
        minHeight: "480px",
        position: "relative",
      }}
    >
      {/* Gold accent line at top */}
      <div
        style={{
          position: "absolute",
          top: 0,
          left: 0,
          right: 0,
          height: "3px",
          background: "var(--color-accent, #d4af37)",
        }}
      />

      <div style={{ maxWidth: "560px" }}>
        <p
          style={{
            fontSize: "11px",
            letterSpacing: "3px",
            textTransform: "uppercase",
            color: "var(--color-accent, #d4af37)",
            marginBottom: "18px",
            fontWeight: 500,
          }}
        >
          {data.tagline}
        </p>
        <h1
          style={{
            fontSize: "52px",
            fontWeight: 300,
            lineHeight: 1.15,
            marginBottom: "20px",
            fontFamily: "var(--font-family, Georgia), serif",
          }}
        >
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        {data.body && (
          <p
            style={{
              fontSize: "17px",
              color: "rgba(255,255,255,0.6)",
              marginBottom: "35px",
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
            background: ctaHover ? "var(--color-accent, #d4af37)" : "transparent",
            color: ctaHover ? "#0a0a0a" : "var(--color-accent, #d4af37)",
            padding: "14px 36px",
            border: "1px solid var(--color-accent, #d4af37)",
            fontSize: "13px",
            fontWeight: 600,
            textDecoration: "none",
            letterSpacing: "2px",
            textTransform: "uppercase",
            transition: "all 0.3s",
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
            overflow: "hidden",
            border: "2px solid var(--color-accent, #d4af37)",
            flexShrink: 0,
            position: "relative",
          }}
        >
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            fill
            style={{ objectFit: "cover" }}
            sizes="320px"
            priority
          />
        </div>
      )}
    </section>
  );
}
