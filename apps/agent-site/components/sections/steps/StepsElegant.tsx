import type { StepsProps } from "@/components/sections/types";

export function StepsElegant({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "var(--color-primary, #0a0a0a)",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 300,
            color: "white",
            marginBottom: subtitle ? "10px" : "60px",
            fontFamily: "var(--font-family, Georgia), serif",
            letterSpacing: "1px",
          }}
        >
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "rgba(255,255,255,0.6)",
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
            flexDirection: "row",
            justifyContent: "center",
            alignItems: "flex-start",
            gap: "0",
            flexWrap: "wrap",
          }}
        >
          {steps.map((step, index) => (
            <div
              key={step.number}
              style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                textAlign: "center",
                flex: "1 1 200px",
                position: "relative",
                padding: "0 20px",
              }}
            >
              {/* Connecting line between steps */}
              {index < steps.length - 1 && (
                <div
                  style={{
                    position: "absolute",
                    top: "30px",
                    left: "calc(50% + 30px)",
                    right: "-30px",
                    height: "1px",
                    background: "var(--color-accent, #d4af37)",
                    opacity: 0.3,
                  }}
                />
              )}
              {/* Number circle */}
              <div
                data-step-circle
                style={{
                  width: "60px",
                  height: "60px",
                  borderRadius: "50%",
                  border: "1px solid var(--color-accent, #d4af37)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "20px",
                  fontWeight: 300,
                  color: "var(--color-accent, #d4af37)",
                  marginBottom: "20px",
                  fontFamily: "var(--font-family, Georgia), serif",
                  flexShrink: 0,
                  position: "relative",
                  zIndex: 1,
                  background: "var(--color-primary, #0a0a0a)",
                }}
              >
                {step.number}
              </div>
              <h3
                style={{
                  fontSize: "18px",
                  fontWeight: 400,
                  color: "white",
                  marginBottom: "10px",
                  fontFamily: "var(--font-family, Georgia), serif",
                }}
              >
                {step.title}
              </h3>
              <p
                style={{
                  fontSize: "14px",
                  color: "rgba(255,255,255,0.6)",
                  lineHeight: 1.7,
                  maxWidth: "200px",
                  margin: "0 auto",
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
