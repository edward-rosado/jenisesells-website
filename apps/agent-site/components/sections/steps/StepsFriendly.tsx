import type { StepsProps } from "@/components/sections/types";

export function StepsFriendly({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
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
            <li
              key={step.number}
              style={{
                display: "flex",
                alignItems: "center",
                gap: "20px",
                background: "white",
                borderRadius: "16px",
                padding: "24px",
                boxShadow: "0 2px 8px rgba(0,0,0,0.06)",
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
          ))}
        </ol>
      </div>
    </section>
  );
}
