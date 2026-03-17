import type { StepsProps } from "@/components/sections/types";

export function StepsTimeline({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
      style={{
        background: "white",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "700px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 600,
          color: "#1a1a1a",
          marginBottom: "8px",
          letterSpacing: "-0.3px",
        }}>
          {title ?? "How It Works"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#888",
            fontSize: "16px",
            marginBottom: "50px",
          }}>
            {subtitle}
          </p>
        )}
        <ol style={{ listStyle: "none", padding: 0, margin: 0 }}>
          {steps.map((step, i) => (
            <li
              key={step.number}
              style={{
                display: "flex",
                gap: "24px",
                alignItems: "flex-start",
                paddingBottom: i < steps.length - 1 ? "40px" : "0",
                position: "relative",
              }}
            >
              {/* Timeline line */}
              {i < steps.length - 1 && (
                <div
                  aria-hidden="true"
                  style={{
                    position: "absolute",
                    left: "19px",
                    top: "40px",
                    width: "2px",
                    height: "calc(100% - 20px)",
                    background: "#e0e0e0",
                  }}
                />
              )}
              {/* Step number */}
              <div
                aria-hidden="true"
                style={{
                  width: "40px",
                  height: "40px",
                  borderRadius: "50%",
                  border: "2px solid var(--color-primary)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "16px",
                  fontWeight: 600,
                  color: "var(--color-primary)",
                  flexShrink: 0,
                  background: "white",
                  position: "relative",
                  zIndex: 1,
                }}
              >
                {step.number}
              </div>
              {/* Content */}
              <div style={{ paddingTop: "6px" }}>
                <h3 style={{
                  color: "#1a1a1a",
                  fontSize: "18px",
                  fontWeight: 600,
                  marginBottom: "4px",
                }}>
                  {step.title}
                </h3>
                <p style={{ color: "#888", fontSize: "14px", lineHeight: 1.6 }}>
                  {step.description}
                </p>
              </div>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}
