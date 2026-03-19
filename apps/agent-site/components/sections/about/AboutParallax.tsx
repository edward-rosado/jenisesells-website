"use client";

import { useRef } from "react";
import { useParallax } from "@/hooks/useParallax";
import type { AboutProps } from "@/components/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/components/sections/types";

export function AboutParallax({ agent, data }: AboutProps) {
  const sectionRef = useRef<HTMLDivElement>(null);
  const bgRef = useRef<HTMLDivElement>(null);

  useParallax(sectionRef, bgRef);

  const name = getDisplayName(agent);
  const headshot = getHeadshotUrl(agent);
  const bgImage = data.image_url ?? headshot;
  const bioArray = Array.isArray(data.bio) ? data.bio : [data.bio];

  return (
    <section id="about">
      <style>{`
        @media (max-width: 768px) {
          [data-about-card] {
            max-width: 100% !important;
            background: rgba(255,255,255,0.96) !important;
          }
        }
      `}</style>
      <div
        ref={sectionRef}
        style={{
          position: "relative" as const,
          minHeight: "80vh",
          overflow: "hidden",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          padding: "80px 24px",
          width: "100%",
          maxWidth: "100vw",
        }}
      >
        {/* Parallax background */}
        <div
          ref={bgRef}
          data-parallax-bg
          style={{
            position: "absolute" as const,
            inset: "-10%",
            backgroundImage: bgImage ? `url('${bgImage}')` : undefined,
            backgroundColor: bgImage ? undefined : "var(--color-primary, #2E7D32)",
            backgroundSize: "cover",
            backgroundPosition: "center",
            willChange: "transform",
          }}
        />

        {/* Overlay */}
        <div style={{
          position: "absolute" as const,
          inset: "0",
          background: "rgba(0,0,0,0.3)",
        }} />

        {/* Content card */}
        <div
          data-about-card
          style={{
            position: "relative" as const,
            zIndex: 2,
            background: "rgba(255,255,255,0.92)",
            backdropFilter: "blur(12px)",
            borderRadius: "16px",
            maxWidth: "560px",
            padding: "48px 40px",
          }}
        >
          <h2 style={{
            fontSize: "28px",
            fontWeight: 700,
            color: "var(--color-primary, #1B5E20)",
            fontFamily: "var(--font-family, inherit)",
            marginBottom: "20px",
          }}>
            {data.title ?? name}
          </h2>

          {bioArray.map((p, i) => (
            <p key={i} style={{
              fontSize: "16px",
              lineHeight: 1.8,
              color: "#444",
              marginBottom: i < bioArray.length - 1 ? "16px" : "0",
            }}>
              {p}
            </p>
          ))}

          {data.credentials && data.credentials.length > 0 && (
            <div style={{
              display: "flex",
              gap: "8px",
              flexWrap: "wrap" as const,
              marginTop: "20px",
            }}>
              {data.credentials.map((cred, i) => (
                <span key={i} style={{
                  background: "var(--color-primary, #1B5E20)",
                  color: "#fff",
                  fontSize: "12px",
                  fontWeight: 600,
                  padding: "4px 12px",
                  borderRadius: "20px",
                  letterSpacing: "1px",
                }}>
                  {cred}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
