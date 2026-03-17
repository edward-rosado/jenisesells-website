import type { StatsProps } from "@/components/sections/types";

export function StatsElegant({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "#f8f6f3",
        padding: "50px 40px",
      }}
    >
      <dl
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "wrap",
          margin: 0,
          gap: 0,
        }}
      >
        {items.map((item, index) => (
          <div
            key={item.label}
            style={{
              display: "flex",
              alignItems: "center",
            }}
          >
            <div
              style={{
                textAlign: "center",
                padding: "0 48px",
              }}
            >
              <dd
                style={{
                  fontSize: "36px",
                  fontWeight: 400,
                  color: "var(--color-accent, #b8926a)",
                  margin: 0,
                  fontFamily: "var(--font-family, Georgia), serif",
                  lineHeight: 1.2,
                }}
              >
                {item.value}
              </dd>
              <dt
                style={{
                  fontSize: "11px",
                  textTransform: "uppercase" as const,
                  letterSpacing: "2px",
                  marginTop: "6px",
                  color: "var(--color-secondary, #5a4a3a)",
                }}
              >
                {item.label}
              </dt>
            </div>
            {index < items.length - 1 && (
              <div
                data-separator
                style={{
                  width: "1px",
                  height: "48px",
                  background: "rgba(184,146,106,0.3)",
                  flexShrink: 0,
                }}
              />
            )}
          </div>
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "rgba(61,48,40,0.4)",
            fontSize: "11px",
            marginTop: "16px",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
