"use client";

import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroAiry({ data, agentPhotoUrl, agentName }: HeroProps) {
  return (
    <section
      style={{
        background: "#ffffff",
        color: "var(--color-primary, #3d3028)",
        paddingTop: "80px",
        paddingBottom: "70px",
        paddingLeft: "40px",
        paddingRight: "40px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "50px",
        flexWrap: "wrap",
        minHeight: "440px",
      }}
    >
      <div style={{ maxWidth: "560px" }}>
        <p
          style={{
            fontSize: "13px",
            letterSpacing: "2px",
            color: "var(--color-accent, #b8926a)",
            marginBottom: "16px",
            fontWeight: 400,
          }}
        >
          {data.tagline}
        </p>
        <h1
          style={{
            fontSize: "44px",
            fontWeight: 300,
            lineHeight: 1.2,
            marginBottom: "20px",
            fontFamily: "var(--font-family, Georgia), serif",
            color: "var(--color-primary, #3d3028)",
          }}
        >
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        {data.body && (
          <p
            style={{
              fontSize: "16px",
              color: "var(--color-secondary, #5a4a3a)",
              marginBottom: "32px",
              lineHeight: 1.7,
            }}
          >
            {data.body}
          </p>
        )}
        <a
          href={safeHref(data.cta_link)}
          style={{
            display: "inline-block",
            background: "var(--color-accent, #b8926a)",
            color: "#ffffff",
            padding: "13px 32px",
            borderRadius: "4px",
            fontSize: "14px",
            fontWeight: 500,
            textDecoration: "none",
            letterSpacing: "0.5px",
          }}
        >
          {data.cta_text}
        </a>
      </div>

      {agentPhotoUrl && (
        <div
          style={{
            width: "100px",
            height: "100px",
            borderRadius: "50%",
            overflow: "hidden",
            border: "2px solid var(--color-accent, #b8926a)",
            flexShrink: 0,
            position: "relative",
          }}
        >
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            fill
            style={{ objectFit: "cover" }}
            sizes="100px"
            priority
          />
        </div>
      )}
    </section>
  );
}
