import type { StatItem } from "@/lib/types";

interface StatsBarProps {
  items: StatItem[];
}

export function StatsBar({ items }: StatsBarProps) {
  return (
    <div
      style={{
        background: "#1B5E20",
        padding: "30px 40px",
        display: "flex",
        justifyContent: "center",
        gap: "50px",
        flexWrap: "wrap",
      }}
    >
      {items.map((item) => (
        <div key={item.label} style={{ textAlign: "center", color: "white" }}>
          <div
            style={{
              fontSize: "32px",
              fontWeight: 800,
              color: "#C8A951",
            }}
          >
            {item.value}
          </div>
          <div
            style={{
              fontSize: "13px",
              textTransform: "uppercase",
              letterSpacing: "1px",
              marginTop: "4px",
            }}
          >
            {item.label}
          </div>
        </div>
      ))}
    </div>
  );
}
