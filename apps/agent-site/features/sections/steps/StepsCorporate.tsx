"use client";

import { useState } from "react";
import type { StepsProps } from "@/features/sections/types";
import type { StepItem } from "@/features/config/types";

function StepsCorporateItem({ step, isLast }: { step: StepItem; isLast: boolean }) {
  const [hover, setHover] = useState(false);
  return (
    <li
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: "24px",
        paddingBottom: !isLast ? "40px" : "0",
        position: "relative",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        borderRadius: "8px",
        padding: !isLast ? "16px 16px 40px" : "16px",
      }}
    >
      {/* Vertical timeline line (not after last item) */}
      {!isLast && (
        <div
          style={{
            position: "absolute",
            left: "39px",
            top: "64px",
            width: "2px",
            bottom: "0",
            background: "#cbd5e1",
          }}
        />
      )}

      {/* Blue circle with step number */}
      <div
        data-testid="step-number"
        aria-hidden="true"
        style={{
          width: "48px",
          height: "48px",
          borderRadius: "50%",
          background: "#2563eb",
          color: "white",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: "18px",
          fontWeight: 800,
          flexShrink: 0,
          zIndex: 1,
          position: "relative",
        }}
      >
        {step.number}
      </div>

      <div style={{ paddingTop: "10px" }}>
        <h3
          style={{
            fontSize: "18px",
            fontWeight: 700,
            color: "#0f172a",
            marginBottom: "6px",
          }}
        >
          {step.title}
        </h3>
        <p style={{ color: "#64748b", fontSize: "15px", lineHeight: 1.6 }}>
          {step.description}
        </p>
      </div>
    </li>
  );
}

export function StepsCorporate({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "#f4f5f7",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "#0f172a",
            marginBottom: subtitle ? "8px" : "48px",
            letterSpacing: "-0.3px",
          }}
        >
          {title ?? "Our Process"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#64748b",
              fontSize: "16px",
              marginBottom: "48px",
            }}
          >
            {subtitle}
          </p>
        )}

        <ol
          role="list"
          style={{
            listStyle: "none",
            padding: 0,
            margin: 0,
            display: "flex",
            flexDirection: "column",
            gap: "0",
            position: "relative",
          }}
        >
          {steps.map((step, idx) => (
            <StepsCorporateItem key={step.number} step={step} isLast={idx === steps.length - 1} />
          ))}
        </ol>
      </div>
    </section>
  );
}
