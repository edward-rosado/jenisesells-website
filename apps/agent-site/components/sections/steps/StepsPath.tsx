"use client";

import { useState } from "react";
import type { StepsProps } from "@/components/sections/types";
import type { StepItem } from "@/features/config/types";

function StepsPathItem({ step, isLast }: { step: StepItem; isLast: boolean }) {
  const [hover, setHover] = useState(false);
  return (
    <li
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: "flex",
        gap: "24px",
        alignItems: "flex-start",
        paddingBottom: !isLast ? "44px" : "0",
        position: "relative",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        borderRadius: "8px",
        padding: !isLast ? "12px 12px 44px" : "12px",
      }}
    >
      {/* Dotted connecting line -- trail marker */}
      {!isLast && (
        <div
          aria-hidden="true"
          style={{
            position: "absolute",
            left: "31px",
            top: "54px",
            width: "2px",
            height: "calc(100% - 34px)",
            borderLeft: "2px dashed var(--color-accent, #4a6741)",
          }}
        />
      )}

      {/* Green trail marker dot */}
      <div
        aria-hidden="true"
        style={{
          width: "40px",
          height: "40px",
          borderRadius: "50%",
          background: "var(--color-accent, #4a6741)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: "16px",
          fontWeight: 700,
          color: "white",
          flexShrink: 0,
          position: "relative",
          zIndex: 1,
          fontFamily: "sans-serif",
        }}
      >
        {step.number}
      </div>

      {/* Content */}
      <div style={{ paddingTop: "6px" }}>
        <h3
          style={{
            color: "var(--color-primary, #2d4a3e)",
            fontSize: "18px",
            fontWeight: 700,
            marginBottom: "6px",
            fontFamily: "Georgia, serif",
          }}
        >
          {step.title}
        </h3>
        <p
          style={{
            color: "#5a5040",
            fontSize: "15px",
            lineHeight: 1.65,
            fontFamily: "sans-serif",
          }}
        >
          {step.description}
        </p>
      </div>
    </li>
  );
}

export function StepsPath({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "var(--color-stone, #e8e2d8)",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "680px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "10px" : "50px",
            fontFamily: "Georgia, serif",
          }}
        >
          {title ?? "Your Path Home"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "var(--color-secondary, #8b6b3d)",
              fontSize: "16px",
              marginBottom: "50px",
              fontFamily: "sans-serif",
            }}
          >
            {subtitle}
          </p>
        )}

        <ol role="list" style={{ listStyle: "none", padding: 0, margin: 0 }}>
          {steps.map((step, i) => (
            <StepsPathItem key={step.number} step={step} isLast={i === steps.length - 1} />
          ))}
        </ol>
      </div>
    </section>
  );
}
