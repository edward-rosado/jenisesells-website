"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/features/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroBold({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      id="hero"
      style={{
        background: "#fafafa",
        color: "#1a1a1a",
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
        overflow: "hidden",
      }}
    >
      {/* Rainbow gradient accent bar at top */}
      <div
        style={{
          position: "absolute",
          top: 0,
          left: 0,
          right: 0,
          height: "4px",
          background: "linear-gradient(90deg, #ff6b6b, #ffd93d, #6bcb77, #4d96ff, #c77dff)",
        }}
      />

      <div style={{ maxWidth: "580px" }}>
        <p
          style={{
            fontSize: "14px",
            fontWeight: 600,
            color: "var(--color-accent, #ff6b6b)",
            marginBottom: "16px",
            letterSpacing: "1px",
            textTransform: "uppercase" as const,
          }}
        >
          {data.tagline}
        </p>
        <h1
          style={{
            fontSize: "58px",
            fontWeight: 800,
            lineHeight: 1.1,
            marginBottom: "20px",
            color: "#1a1a1a",
            fontFamily: "var(--font-family, Inter), sans-serif",
          }}
        >
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        {data.body && (
          <p
            style={{
              fontSize: "17px",
              color: "#555",
              marginBottom: "32px",
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
              ? "var(--color-primary, #1a1a1a)"
              : "var(--color-accent, #ff6b6b)",
            color: "white",
            padding: "14px 32px",
            borderRadius: "8px",
            fontSize: "16px",
            fontWeight: 700,
            textDecoration: "none",
            transition: "all 0.2s",
            boxShadow: ctaHover
              ? "0 6px 20px rgba(0,0,0,0.25)"
              : "0 4px 12px rgba(255,107,107,0.35)",
          }}
        >
          {data.cta_text}
        </a>
      </div>

      {agentPhotoUrl && (
        <div
          data-photo-wrapper
          style={{
            width: "280px",
            height: "280px",
            borderRadius: "50%",
            overflow: "hidden",
            position: "relative",
            flexShrink: 0,
            border: "4px solid var(--color-accent, #ff6b6b)",
          }}
        >
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            fill
            style={{ objectFit: "cover" }}
            sizes="280px"
            priority
          />
        </div>
      )}
    </section>
  );
}
