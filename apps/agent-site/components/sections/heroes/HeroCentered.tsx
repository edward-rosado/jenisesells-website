"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroCentered({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      style={{
        background: "#FFF8F0",
        color: "#4A3728",
        paddingTop: "100px",
        paddingBottom: "70px",
        paddingLeft: "40px",
        paddingRight: "40px",
        textAlign: "center",
        minHeight: "400px",
      }}
    >
      {agentPhotoUrl && (
        <div style={{
          width: "200px",
          height: "200px",
          borderRadius: "50%",
          overflow: "hidden",
          border: "5px solid var(--color-accent)",
          margin: "0 auto 24px",
          boxShadow: "0 8px 24px rgba(0,0,0,0.1)",
        }}>
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            width={200}
            height={200}
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
            priority
          />
        </div>
      )}
      <h1 style={{
        fontSize: "40px",
        fontWeight: 700,
        lineHeight: 1.2,
        marginBottom: "12px",
        color: "#4A3728",
        maxWidth: "600px",
        marginLeft: "auto",
        marginRight: "auto",
      }}>
        {renderHeadline(data.headline, data.highlight_word)}
      </h1>
      <p style={{
        fontSize: "18px",
        color: "#8B7355",
        marginBottom: "16px",
        fontStyle: "italic",
      }}>
        {data.tagline}
      </p>
      {data.body && (
        <p style={{
          fontSize: "16px",
          color: "#8B7355",
          marginBottom: "28px",
          maxWidth: "500px",
          marginLeft: "auto",
          marginRight: "auto",
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
          background: ctaHover ? "var(--color-primary)" : "var(--color-accent)",
          color: ctaHover ? "white" : "var(--color-primary)",
          padding: "14px 36px",
          borderRadius: "30px",
          fontSize: "16px",
          fontWeight: 700,
          textDecoration: "none",
          transition: "all 0.3s",
          transform: ctaHover ? "translateY(-2px)" : "none",
          boxShadow: ctaHover ? "0 6px 20px rgba(0,0,0,0.15)" : "0 2px 8px rgba(0,0,0,0.08)",
        }}
      >
        {data.cta_text} &rarr;
      </a>
    </section>
  );
}
