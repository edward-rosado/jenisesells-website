"use client";

import { useState } from "react";
import type { StepsProps } from "@/components/sections/types";
import type { StepItem } from "@/lib/types";

function StepsRefinedItem({ step, isLast }: { step: StepItem; isLast: boolean }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: "28px",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        borderRadius: "8px",
        padding: "12px",
      }}
    >
      {/* Circle + line column */}
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          flexShrink: 0,
        }}
      >
        <div
          data-step-circle
          style={{
            width: "52px",
            height: "52px",
            borderRadius: "50%",
            border: "1px solid var(--color-accent, #b8926a)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: "18px",
            fontWeight: 300,
            color: "var(--color-accent, #b8926a)",
            fontFamily: "var(--font-family, Georgia), serif",
            flexShrink: 0,
          }}
        >
          {step.number}
        </div>
        {!isLast && (
          <div
            data-step-line
            style={{
              width: "1px",
              flexGrow: 1,
              minHeight: "40px",
              background: "rgba(184,146,106,0.25)",
              margin: "8px 0",
            }}
          />
        )}
      </div>

      {/* Content */}
      <div style={{ paddingBottom: !isLast ? "32px" : "0" }}>
        <h3
          style={{
            fontSize: "20px",
            fontWeight: 400,
            color: "var(--color-primary, #3d3028)",
            marginBottom: "8px",
            marginTop: "12px",
            fontFamily: "var(--font-family, Georgia), serif",
          }}
        >
          {step.title}
        </h3>
        <p
          style={{
            fontSize: "15px",
            color: "var(--color-secondary, #5a4a3a)",
            lineHeight: 1.7,
            margin: 0,
          }}
        >
          {step.description}
        </p>
      </div>
    </div>
  );
}

export function StepsRefined({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "#ffffff",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "700px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 300,
            color: "var(--color-primary, #3d3028)",
            marginBottom: subtitle ? "10px" : "60px",
            fontFamily: "var(--font-family, Georgia), serif",
            letterSpacing: "1px",
          }}
        >
          {title ?? "The Process"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "var(--color-secondary, #5a4a3a)",
              fontSize: "16px",
              marginBottom: "60px",
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: "0",
          }}
        >
          {steps.map((step, index) => (
            <StepsRefinedItem key={step.number} step={step} isLast={index === steps.length - 1} />
          ))}
        </div>
      </div>
    </section>
  );
}
