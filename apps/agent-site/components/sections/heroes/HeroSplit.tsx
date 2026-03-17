"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroSplit({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      style={{
        background: "#fafafa",
        color: "#1a1a1a",
        paddingTop: "100px",
        paddingBottom: "80px",
        paddingLeft: "60px",
        paddingRight: "60px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "60px",
        flexWrap: "wrap",
        minHeight: "400px",
      }}
    >
      <div style={{ maxWidth: "480px", flex: 1 }}>
        <h1 style={{
          fontSize: "48px",
          fontWeight: 700,
          lineHeight: 1.1,
          letterSpacing: "-0.5px",
          marginBottom: "16px",
          color: "#1a1a1a",
        }}>
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        <p style={{
          fontSize: "18px",
          color: "#666",
          marginBottom: "20px",
          lineHeight: 1.6,
        }}>
          {data.tagline}
        </p>
        {data.body && (
          <p style={{
            fontSize: "16px",
            color: "#888",
            marginBottom: "28px",
            lineHeight: 1.6,
          }}>
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
            background: ctaHover ? "var(--color-primary)" : "#1a1a1a",
            color: "white",
            padding: "14px 32px",
            borderRadius: "30px",
            fontSize: "15px",
            fontWeight: 600,
            textDecoration: "none",
            transition: "all 0.3s",
            transform: ctaHover ? "translateY(-2px)" : "none",
          }}
        >
          {data.cta_text} →
        </a>
      </div>
      {agentPhotoUrl && (
        <div style={{
          width: "320px",
          height: "380px",
          borderRadius: "16px",
          overflow: "hidden",
          flexShrink: 0,
        }}>
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            width={320}
            height={380}
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
            priority
          />
        </div>
      )}
    </section>
  );
}
