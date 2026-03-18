"use client";

import { useState } from "react";
import type { StepsProps } from "@/components/sections/types";
import type { StepItem } from "@/lib/types";

function StepsJourneyItem({ step, isLast }: { step: StepItem; isLast: boolean }) {
  const [hover, setHover] = useState(false);
  return (
    <li
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: "20px",
        position: "relative",
        paddingBottom: "0",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        borderRadius: "8px",
        padding: "12px",
      }}
    >
      {/* Vertical connector line */}
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          flexShrink: 0,
        }}
      >
        {/* Circle number */}
        <div
          style={{
            width: "48px",
            height: "48px",
            borderRadius: "50%",
            background: "var(--color-accent, #5a9e7c)",
            color: "white",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: "20px",
            fontWeight: 700,
            flexShrink: 0,
            zIndex: 1,
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {step.number}
        </div>
        {/* Connecting line between steps */}
        {!isLast && (
          <div
            aria-hidden="true"
            style={{
              width: "2px",
              flexGrow: 1,
              minHeight: "40px",
              background: "var(--color-accent, #5a9e7c)",
              opacity: 0.3,
              margin: "4px 0",
            }}
          />
        )}
      </div>

      {/* Step content */}
      <div
        style={{
          paddingTop: "10px",
          paddingBottom: !isLast ? "32px" : "0",
        }}
      >
        <h3
          style={{
            fontSize: "20px",
            fontWeight: 600,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: "8px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {step.title}
        </h3>
        <p
          style={{
            fontSize: "15px",
            color: "#4a6b5a",
            lineHeight: 1.7,
            margin: 0,
          }}
        >
          {step.description}
        </p>
      </div>
    </li>
  );
}

export function StepsJourney({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "#f0f7f4",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "700px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 600,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "8px" : "48px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {title ?? "Your Journey Home"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#4a6b5a",
              fontSize: "16px",
              marginBottom: "48px",
              lineHeight: 1.6,
            }}
          >
            {subtitle}
          </p>
        )}
        <ol
          style={{
            listStyle: "none",
            padding: 0,
            margin: 0,
            display: "flex",
            flexDirection: "column",
            gap: "0",
          }}
        >
          {steps.map((step, idx) => (
            <StepsJourneyItem key={step.number} step={step} isLast={idx === steps.length - 1} />
          ))}
        </ol>
      </div>
    </section>
  );
}
