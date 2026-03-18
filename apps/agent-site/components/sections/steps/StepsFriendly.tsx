"use client";

import { useState } from "react";
import type { StepsProps } from "@/components/sections/types";

function StepCardFriendly({ step }: { step: { number: number; title: string; description: string } }) {
  const [hover, setHover] = useState(false);

  return (
    <li
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        display: "flex",
        alignItems: "center",
        gap: "20px",
        background: "white",
        borderRadius: "16px",
        padding: "24px",
        boxShadow: hover
          ? "0 6px 20px rgba(0,0,0,0.12)"
          : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
      }}
    >
      <div style={{
        width: "44px",
        height: "44px",
        borderRadius: "12px",
        background: "var(--color-accent)",
        color: "white",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        fontWeight: 700,
        fontSize: "18px",
        flexShrink: 0,
      }}>
        {step.number}
      </div>
      <div>
        <h3 style={{
          fontSize: "18px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: "4px",
        }}>
          {step.title}
        </h3>
        <p style={{
          fontSize: "14px",
          color: "#8B7355",
          lineHeight: 1.5,
          margin: 0,
        }}>
          {step.description}
        </p>
      </div>
    </li>
  );
}

export function StepsFriendly({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        padding: "70px 40px",
        background: "#FFF8F0",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: subtitle ? "8px" : "40px",
        }}>
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#8B7355",
            fontSize: "16px",
            marginBottom: "40px",
          }}>
            {subtitle}
          </p>
        )}
        <ol style={{
          display: "flex",
          flexDirection: "column",
          gap: "20px",
          listStyle: "none",
          padding: 0,
          margin: 0,
        }}>
          {steps.map((step) => (
            <StepCardFriendly key={step.number} step={step} />
          ))}
        </ol>
      </div>
    </section>
  );
}
