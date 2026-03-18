"use client";

import Image from "next/image";
import type { HeroProps } from "@/components/sections/types";
import { safeHref, renderHeadline } from "./hero-utils";

export function HeroCoastal({ data, agentPhotoUrl, agentName }: HeroProps) {
  return (
    <section
      style={{
        background: "#fefcf8",
        color: "var(--color-primary, #2c7a7b)",
        paddingTop: "80px",
        paddingBottom: "70px",
        paddingLeft: "40px",
        paddingRight: "40px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "50px",
        flexWrap: "wrap",
        minHeight: "520px",
      }}
    >
      <div style={{ maxWidth: "580px" }}>
        <p
          style={{
            fontSize: "13px",
            letterSpacing: "2px",
            color: "var(--color-secondary, #b7791f)",
            marginBottom: "16px",
            fontWeight: 500,
            textTransform: "uppercase",
          }}
        >
          {data.tagline}
        </p>
        <h1
          style={{
            fontSize: "46px",
            fontWeight: 700,
            lineHeight: 1.2,
            marginBottom: "20px",
            color: "var(--color-primary, #2c7a7b)",
          }}
        >
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        {data.body && (
          <p
            style={{
              fontSize: "16px",
              color: "#4a6c6c",
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
            background: "var(--color-primary, #2c7a7b)",
            color: "#ffffff",
            padding: "14px 36px",
            borderRadius: "30px",
            fontSize: "15px",
            fontWeight: 600,
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
            width: "360px",
            height: "420px",
            borderRadius: "20px",
            overflow: "hidden",
            border: "4px solid var(--color-primary, #2c7a7b)",
            flexShrink: 0,
            position: "relative",
            boxShadow: "0 12px 40px rgba(44,122,123,0.18)",
          }}
        >
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            fill
            style={{ objectFit: "cover" }}
            sizes="360px"
            priority
          />
        </div>
      )}
    </section>
  );
}
