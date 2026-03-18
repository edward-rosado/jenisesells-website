import type { StepsProps } from "@/components/sections/types";

export function StepsCards({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "white",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 800,
            color: "var(--color-primary, #1a1a1a)",
            marginBottom: "10px",
            fontFamily: "var(--font-family, Inter), sans-serif",
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
              marginBottom: "50px",
            }}
          >
            {subtitle}
          </p>
        )}
        {!subtitle && <div style={{ marginBottom: "50px" }} />}

        <div
          data-steps-row
          style={{
            display: "flex",
            gap: "24px",
            flexWrap: "wrap",
            justifyContent: "center",
          }}
        >
          {steps.map((step) => (
            <div
              key={step.number}
              style={{
                background: "#fafafa",
                borderRadius: "16px",
                padding: "32px 28px",
                flex: "1 1 250px",
                maxWidth: "320px",
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
                position: "relative",
                overflow: "hidden",
              }}
            >
              {/* Watermark step number */}
              <span
                data-step-watermark
                aria-hidden="true"
                style={{
                  position: "absolute",
                  top: "-10px",
                  right: "12px",
                  fontSize: "96px",
                  fontWeight: 900,
                  color: "var(--color-accent, #ff6b6b)",
                  opacity: 0.08,
                  lineHeight: 1,
                  userSelect: "none",
                  fontFamily: "var(--font-family, Inter), sans-serif",
                }}
              >
                {String(step.number).padStart(2, "0")}
              </span>

              <h3
                style={{
                  fontSize: "18px",
                  fontWeight: 700,
                  color: "var(--color-primary, #1a1a1a)",
                  marginBottom: "12px",
                  fontFamily: "var(--font-family, Inter), sans-serif",
                  position: "relative",
                }}
              >
                {step.title}
              </h3>
              <p
                style={{
                  fontSize: "15px",
                  color: "#666",
                  lineHeight: 1.6,
                  position: "relative",
                }}
              >
                {step.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
