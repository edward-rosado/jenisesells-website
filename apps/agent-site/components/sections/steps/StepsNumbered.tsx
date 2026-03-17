import type { StepsProps } from "@/components/sections/types";

export function StepsNumbered({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
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
            <li
              key={step.number}
              style={{
                textAlign: "center",
                maxWidth: "250px",
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
          ))}
        </ol>
      </div>
    </section>
  );
}
