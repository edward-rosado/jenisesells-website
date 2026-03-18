import type { StepsProps } from "@/components/sections/types";

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
            <li
              key={step.number}
              style={{
                textAlign: "center",
                maxWidth: "280px",
                flex: "1 1 200px",
                padding: "0 20px",
                position: "relative",
              }}
            >
              {/* Connecting line between steps */}
              {index < steps.length - 1 && (
                <div
                  aria-hidden="true"
                  style={{
                    position: "absolute",
                    top: "30px",
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
          ))}
        </ol>
      </div>
    </section>
  );
}
