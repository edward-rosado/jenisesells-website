import type { StatsProps } from "@/components/sections/types";

export function StatsWave({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "var(--color-primary, #2c7a7b)",
        color: "white",
        padding: "50px 40px",
      }}
    >
      <dl
        style={{
          display: "flex",
          justifyContent: "center",
          gap: "60px",
          flexWrap: "wrap",
          margin: 0,
        }}
      >
        {items.map((item) => (
          <div key={item.label} style={{ textAlign: "center" }}>
            <dd
              style={{
                fontSize: "34px",
                fontWeight: 800,
                color: "var(--color-accent, #e8f4f8)",
                margin: 0,
                marginBottom: "6px",
              }}
            >
              {item.value}
            </dd>
            <dt
              style={{
                fontSize: "13px",
                textTransform: "uppercase",
                letterSpacing: "1.5px",
                color: "rgba(255,255,255,0.85)",
              }}
            >
              {item.label}
            </dt>
          </div>
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.6)",
            fontSize: "11px",
            marginTop: "16px",
            marginBottom: 0,
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
