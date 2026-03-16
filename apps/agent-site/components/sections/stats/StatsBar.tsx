import type { StatsProps } from "@/components/sections/types";

export function StatsBar({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      aria-label="Agent statistics"
      style={{
        background: "#1B5E20",
        padding: "30px 40px",
      }}
    >
      <dl
        style={{
          display: "flex",
          justifyContent: "center",
          gap: "50px",
          flexWrap: "wrap",
          margin: 0,
        }}
      >
        {items.map((item) => (
          <div key={item.label} style={{ textAlign: "center", color: "white" }}>
            <dt
              style={{
                fontSize: "13px",
                textTransform: "uppercase",
                letterSpacing: "1px",
                marginTop: "4px",
                order: 2,
              }}
            >
              {item.label}
            </dt>
            <dd
              style={{
                fontSize: "32px",
                fontWeight: 800,
                color: "#C8A951",
                margin: 0,
              }}
            >
              {item.value}
            </dd>
          </div>
        ))}
      </dl>
      {sourceDisclaimer && (
        <p
          style={{
            textAlign: "center",
            color: "rgba(255,255,255,0.6)",
            fontSize: "11px",
            marginTop: "12px",
          }}
        >
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
