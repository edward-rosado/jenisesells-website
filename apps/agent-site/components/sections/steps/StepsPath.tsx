import type { StepsProps } from "@/components/sections/types";

export function StepsPath({ steps, title, subtitle }: StepsProps) {
  return (
    <section
      id="steps"
      style={{
        background: "var(--color-stone, #e8e2d8)",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "680px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "10px" : "50px",
            fontFamily: "Georgia, serif",
          }}
        >
          {title ?? "Your Path Home"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "var(--color-secondary, #8b6b3d)",
              fontSize: "16px",
              marginBottom: "50px",
              fontFamily: "sans-serif",
            }}
          >
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
                paddingBottom: i < steps.length - 1 ? "44px" : "0",
                position: "relative",
              }}
            >
              {/* Dotted connecting line — trail marker */}
              {i < steps.length - 1 && (
                <div
                  aria-hidden="true"
                  style={{
                    position: "absolute",
                    left: "19px",
                    top: "42px",
                    width: "2px",
                    height: "calc(100% - 22px)",
                    borderLeft: "2px dashed var(--color-accent, #4a6741)",
                  }}
                />
              )}

              {/* Green trail marker dot */}
              <div
                aria-hidden="true"
                style={{
                  width: "40px",
                  height: "40px",
                  borderRadius: "50%",
                  background: "var(--color-accent, #4a6741)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: "16px",
                  fontWeight: 700,
                  color: "white",
                  flexShrink: 0,
                  position: "relative",
                  zIndex: 1,
                  fontFamily: "sans-serif",
                }}
              >
                {step.number}
              </div>

              {/* Content */}
              <div style={{ paddingTop: "6px" }}>
                <h3
                  style={{
                    color: "var(--color-primary, #2d4a3e)",
                    fontSize: "18px",
                    fontWeight: 700,
                    marginBottom: "6px",
                    fontFamily: "Georgia, serif",
                  }}
                >
                  {step.title}
                </h3>
                <p
                  style={{
                    color: "#5a5040",
                    fontSize: "15px",
                    lineHeight: 1.65,
                    fontFamily: "sans-serif",
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
