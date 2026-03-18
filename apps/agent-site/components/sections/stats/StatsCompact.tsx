import type { StatsProps } from "@/components/sections/types";

export function StatsCompact({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Agent statistics"
      style={{
        background: "#f0f0f0",
        padding: "32px 40px",
      }}
    >
      <div
        data-stats-row
        style={{
          display: "flex",
          justifyContent: "center",
          gap: "16px",
          flexWrap: "wrap",
          margin: "0 auto",
          maxWidth: "900px",
        }}
      >
        {items.map((item) => (
          <div
            key={item.label}
            data-stat-pill
            style={{
              background: "#1a1a1a",
              borderRadius: "24px",
              padding: "12px 24px",
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: "4px",
              minWidth: "130px",
            }}
          >
            <span
              style={{
                fontSize: "28px",
                fontWeight: 800,
                color: "var(--color-accent, #ff6b6b)",
                lineHeight: 1.1,
              }}
            >
              {item.value}
            </span>
            <span
              style={{
                fontSize: "12px",
                color: "rgba(255,255,255,0.7)",
                textTransform: "uppercase" as const,
                letterSpacing: "1px",
                fontWeight: 500,
              }}
            >
              {item.label}
            </span>
          </div>
        ))}
      </div>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "#999",
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
