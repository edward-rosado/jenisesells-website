import type { StatsProps } from "@/components/sections/types";

export function StatsWarm({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      style={{
        background: "#f0f7f4",
        padding: "50px 40px",
      }}
    >
      <div
        style={{
          maxWidth: "1000px",
          margin: "0 auto",
          display: "flex",
          flexWrap: "wrap",
          justifyContent: "center",
          gap: "20px",
        }}
      >
        {items.map((item) => (
          <div
            key={item.label}
            style={{
              background: "white",
              borderRadius: "16px",
              padding: "28px 36px",
              textAlign: "center",
              boxShadow: "0 4px 16px rgba(90,158,124,0.1)",
              minWidth: "160px",
            }}
          >
            <dl style={{ margin: 0 }}>
              <dd
                style={{
                  fontSize: "36px",
                  fontWeight: 700,
                  color: "var(--color-accent, #5a9e7c)",
                  margin: 0,
                  fontFamily: "var(--font-family, Nunito), sans-serif",
                }}
              >
                {item.value}
              </dd>
              <dt
                style={{
                  fontSize: "13px",
                  color: "#4a6b5a",
                  marginTop: "6px",
                  fontWeight: 600,
                  letterSpacing: "0.5px",
                }}
              >
                {item.label}
              </dt>
            </dl>
          </div>
        ))}
      </div>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "#7a9a88",
            fontSize: "11px",
            marginTop: "20px",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
