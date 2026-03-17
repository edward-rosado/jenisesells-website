import type { StepsProps } from "@/components/sections/types";

export function StepsCorporate({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
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
            <li
              key={step.number}
              style={{
                display: "flex",
                alignItems: "flex-start",
                gap: "24px",
                paddingBottom: idx < steps.length - 1 ? "40px" : "0",
                position: "relative",
              }}
            >
              {/* Vertical timeline line (not after last item) */}
              {idx < steps.length - 1 && (
                <div
                  style={{
                    position: "absolute",
                    left: "23px",
                    top: "48px",
                    width: "2px",
                    bottom: "0",
                    background: "#cbd5e1",
                  }}
                />
              )}

              {/* Blue circle with step number */}
              <div
                data-testid="step-number"
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
          ))}
        </ol>
      </div>
    </section>
  );
}
