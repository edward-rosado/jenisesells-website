import type { StepsProps } from "@/components/sections/types";

export function StepsJourney({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="how-it-works"
      style={{
        background: "#f0f7f4",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "700px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 600,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "8px" : "48px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {title ?? "Your Journey Home"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#4a6b5a",
              fontSize: "16px",
              marginBottom: "48px",
              lineHeight: 1.6,
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
          }}
        >
          {steps.map((step, idx) => (
            <li
              key={step.number}
              style={{
                display: "flex",
                alignItems: "flex-start",
                gap: "20px",
                position: "relative",
                paddingBottom: idx < steps.length - 1 ? "0" : "0",
              }}
            >
              {/* Vertical connector line */}
              <div
                style={{
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  flexShrink: 0,
                }}
              >
                {/* Circle number */}
                <div
                  style={{
                    width: "48px",
                    height: "48px",
                    borderRadius: "50%",
                    background: "var(--color-accent, #5a9e7c)",
                    color: "white",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontSize: "20px",
                    fontWeight: 700,
                    flexShrink: 0,
                    zIndex: 1,
                    fontFamily: "var(--font-family, Nunito), sans-serif",
                  }}
                >
                  {step.number}
                </div>
                {/* Connecting line between steps */}
                {idx < steps.length - 1 && (
                  <div
                    aria-hidden="true"
                    style={{
                      width: "2px",
                      flexGrow: 1,
                      minHeight: "40px",
                      background: "var(--color-accent, #5a9e7c)",
                      opacity: 0.3,
                      margin: "4px 0",
                    }}
                  />
                )}
              </div>

              {/* Step content */}
              <div
                style={{
                  paddingTop: "10px",
                  paddingBottom: idx < steps.length - 1 ? "32px" : "0",
                }}
              >
                <h3
                  style={{
                    fontSize: "20px",
                    fontWeight: 600,
                    color: "var(--color-primary, #2d4a3e)",
                    marginBottom: "8px",
                    fontFamily: "var(--font-family, Nunito), sans-serif",
                  }}
                >
                  {step.title}
                </h3>
                <p
                  style={{
                    fontSize: "15px",
                    color: "#4a6b5a",
                    lineHeight: 1.7,
                    margin: 0,
                  }}
                >
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
