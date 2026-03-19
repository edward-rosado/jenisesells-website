"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroGradient({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      id="hero"
      style={{
        background: "linear-gradient(135deg, var(--color-primary) 0%, var(--color-secondary) 60%, #43A047 100%)",
        color: "white",
        paddingTop: "80px",
        paddingBottom: "60px",
        paddingLeft: "40px",
        paddingRight: "40px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "40px",
        flexWrap: "wrap",
        minHeight: "400px",
      }}
    >
      <div style={{ maxWidth: "520px" }}>
        <h1 style={{ fontSize: "44px", fontWeight: 800, lineHeight: 1.15, marginBottom: "10px" }}>
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        <p style={{ fontSize: "20px", color: "#C8E6C9", marginBottom: "25px", fontStyle: "italic" }}>
          {data.tagline}
        </p>
        {data.body && (
          <p style={{ fontSize: "17px", color: "#E8F5E9", marginBottom: "30px" }}>
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
            background: ctaHover ? "white" : "var(--color-accent)",
            color: "var(--color-primary)",
            padding: "16px 36px",
            borderRadius: "30px",
            fontSize: "17px",
            fontWeight: 700,
            textDecoration: "none",
            transition: "all 0.3s",
            transform: ctaHover ? "translateY(-2px)" : "none",
            boxShadow: ctaHover ? "0 8px 25px rgba(0,0,0,0.3)" : "none",
          }}
        >
          {data.cta_text} <span aria-hidden="true">&rarr;</span>
        </a>
      </div>
      {agentPhotoUrl && (
        <div
          style={{
            width: "300px",
            height: "300px",
            borderRadius: "50%",
            overflow: "hidden",
            border: "5px solid var(--color-accent)",
            boxShadow: "0 15px 40px rgba(0,0,0,0.3)",
          }}
        >
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            width={300}
            height={300}
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
            priority
          />
        </div>
      )}
    </section>
  );
}
