"use client";

import { useState } from "react";
import type { StepsProps } from "@/components/sections/types";
import type { StepItem } from "@/lib/types";

function StepsNumberedItem({ step }: { step: StepItem }) {
  const [hover, setHover] = useState(false);
  return (
    <li
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        textAlign: "center",
        maxWidth: "250px",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        borderRadius: "12px",
        padding: "20px",
      }}
    >
      <div
        aria-hidden="true"
        style={{
          width: "60px",
          height: "60px",
          background: "var(--color-secondary)",
          color: "white",
          borderRadius: "50%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: "24px",
          fontWeight: 700,
          margin: "0 auto 15px",
        }}
      >
        {step.number}
      </div>
      <h3 style={{ color: "var(--color-primary)", marginBottom: "8px" }}>
        {step.title}
      </h3>
      <p style={{ color: "#666", fontSize: "14px" }}>
        {step.description}
      </p>
    </li>
  );
}

export function StepsNumbered({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "#f5f5f5",
        maxWidth: "100%",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary)",
            marginBottom: "10px",
          }}
        >
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#666",
              fontSize: "16px",
              marginBottom: "45px",
            }}
          >
            {subtitle}
          </p>
        )}
        <ol
          role="list"
          style={{
            display: "flex",
            justifyContent: "center",
            gap: "40px",
            flexWrap: "wrap",
            listStyle: "none",
            padding: 0,
            margin: 0,
          }}
        >
          {steps.map((step) => (
            <StepsNumberedItem key={step.number} step={step} />
          ))}
        </ol>
      </div>
    </section>
  );
}
