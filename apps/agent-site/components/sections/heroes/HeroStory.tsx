"use client";

import { useState } from "react";
import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroStory({ data, agentPhotoUrl, agentName }: HeroProps) {
  const [ctaHover, setCtaHover] = useState(false);

  return (
    <section
      style={{
        background: "#f0f7f4",
        color: "var(--color-primary, #2d4a3e)",
        paddingTop: "100px",
        paddingBottom: "80px",
        paddingLeft: "40px",
        paddingRight: "40px",
        minHeight: "420px",
        position: "relative",
        overflow: "hidden",
      }}
    >
      {/* Warm green gradient overlay */}
      <div
        aria-hidden="true"
        style={{
          position: "absolute",
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background:
            "linear-gradient(135deg, rgba(90,158,124,0.08) 0%, rgba(240,247,244,0) 60%)",
          pointerEvents: "none",
        }}
      />

      <div
        style={{
          maxWidth: "700px",
          margin: "0 auto",
          position: "relative",
          zIndex: 1,
        }}
      >
        {/* Small agent photo badge */}
        {agentPhotoUrl && (
          <div
            style={{
              width: "80px",
              height: "80px",
              borderRadius: "50%",
              overflow: "hidden",
              border: "3px solid var(--color-accent, #5a9e7c)",
              marginBottom: "24px",
              boxShadow: "0 4px 12px rgba(0,0,0,0.1)",
            }}
          >
            <Image
              src={agentPhotoUrl}
              alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
              width={80}
              height={80}
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
              priority
            />
          </div>
        )}

        <h1
          style={{
            fontSize: "44px",
            fontWeight: 600,
            lineHeight: 1.2,
            marginBottom: "16px",
            color: "var(--color-primary, #2d4a3e)",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>

        <p
          style={{
            fontSize: "20px",
            color: "var(--color-accent, #5a9e7c)",
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
              color: "#4a6b5a",
              marginBottom: "32px",
              maxWidth: "560px",
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
          {data.cta_text} &rarr;
        </a>
      </div>
    </section>
  );
}
