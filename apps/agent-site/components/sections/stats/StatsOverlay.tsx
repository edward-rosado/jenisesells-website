import type { StatsProps } from "@/components/sections/types";

export function StatsOverlay({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "rgba(0,0,0,0.85)",
        padding: "40px",
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
          <div key={item.label} style={{ textAlign: "center", color: "white" }}>
            <dd
              style={{
                fontSize: "36px",
                fontWeight: 700,
                color: "var(--color-accent, #d4af37)",
                margin: 0,
                fontFamily: "var(--font-family, Georgia), serif",
              }}
            >
              {item.value}
            </dd>
            <dt
              style={{
                fontSize: "11px",
                textTransform: "uppercase",
                letterSpacing: "2px",
                marginTop: "6px",
                color: "rgba(255,255,255,0.6)",
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
            color: "rgba(255,255,255,0.4)",
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
