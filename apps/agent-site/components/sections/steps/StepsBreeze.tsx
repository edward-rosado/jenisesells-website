"use client";

import { useState } from "react";
import type { StepsProps } from "@/components/sections/types";
import type { StepItem } from "@/features/config/types";

function StepsBreezeItem({ step, isLast }: { step: StepItem; isLast: boolean }) {
  const [hover, setHover] = useState(false);
  return (
    <li
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        textAlign: "center",
        maxWidth: "280px",
        flex: "1 1 200px",
        padding: "0 20px",
        position: "relative",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
        borderRadius: "12px",
        paddingTop: "20px",
        paddingBottom: "20px",
      }}
    >
      {/* Connecting line between steps */}
      {!isLast && (
        <div
          aria-hidden="true"
          style={{
            position: "absolute",
            top: "50px",
            right: "-10px",
            width: "20px",
            height: "2px",
            background: "var(--color-primary, #2c7a7b)",
            opacity: 0.3,
          }}
        />
      )}
      <div
        aria-hidden="true"
        style={{
          width: "60px",
          height: "60px",
          background: "var(--color-primary, #2c7a7b)",
          color: "white",
          borderRadius: "50%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: "22px",
          fontWeight: 700,
          margin: "0 auto 18px",
        }}
      >
        {step.number}
      </div>
      <h3
        style={{
          fontSize: "17px",
          fontWeight: 700,
          color: "var(--color-primary, #2c7a7b)",
          marginBottom: "8px",
        }}
      >
        {step.title}
      </h3>
      <p
        style={{
          fontSize: "14px",
          color: "#4a6c6c",
          lineHeight: 1.6,
          margin: 0,
        }}
      >
        {step.description}
      </p>
    </li>
  );
}

export function StepsBreeze({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "#fefcf8",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2c7a7b)",
            marginBottom: subtitle ? "8px" : "50px",
          }}
        >
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#4a6c6c",
              fontSize: "16px",
              marginBottom: "50px",
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
            gap: "0",
            flexWrap: "wrap",
            listStyle: "none",
            padding: 0,
            margin: 0,
            position: "relative",
          }}
        >
          {steps.map((step, index) => (
            <StepsBreezeItem key={step.number} step={step} isLast={index === steps.length - 1} />
          ))}
        </ol>
      </div>
    </section>
  );
}
