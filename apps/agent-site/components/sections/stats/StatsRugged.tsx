import type { StatsProps } from "@/components/sections/types";

export function StatsRugged({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "var(--color-primary, #2d4a3e)",
        padding: "40px 40px",
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
                fontSize: "36px",
                fontWeight: 800,
                color: "var(--color-accent, #c8a84b)",
                margin: 0,
                fontFamily: "Georgia, serif",
              }}
            >
              {item.value}
            </dd>
            <dt
              style={{
                fontSize: "12px",
                textTransform: "uppercase",
                letterSpacing: "1.5px",
                color: "rgba(255,255,255,0.80)",
                marginTop: "6px",
                fontFamily: "sans-serif",
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
            color: "rgba(255,255,255,0.55)",
            fontSize: "11px",
            marginTop: "14px",
            fontFamily: "sans-serif",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
