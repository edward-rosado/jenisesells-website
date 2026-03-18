import type { StatsProps } from "@/components/sections/types";

export function StatsMetrics({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      id="stats"
      aria-label="Key metrics"
      style={{
        background: "#f4f5f7",
        padding: "60px 40px",
      }}
    >
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
          gap: "20px",
          maxWidth: "1100px",
          margin: "0 auto",
        }}
      >
        {items.map((item) => (
          <div
            key={item.label}
            data-testid="stat-card"
            style={{
              background: "white",
              borderRadius: "6px",
              padding: "28px 24px",
              textAlign: "center",
              border: "1px solid #e2e8f0",
              boxShadow: "0 1px 4px rgba(0,0,0,0.05)",
            }}
          >
            <div
              data-testid="stat-value"
              style={{
                fontSize: "32px",
                fontWeight: 800,
                color: "#2563eb",
                lineHeight: 1,
                marginBottom: "8px",
              }}
            >
              {item.value}
            </div>
            <div
              style={{
                fontSize: "13px",
                color: "#64748b",
                textTransform: "uppercase",
                letterSpacing: "0.8px",
                fontWeight: 600,
              }}
            >
              {item.label}
            </div>
          </div>
        ))}
      </div>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "#94a3b8",
            fontSize: "11px",
            marginTop: "20px",
            maxWidth: "1100px",
            margin: "16px auto 0",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
