import type { StatsProps } from "@/components/sections/types";

export function StatsInline({ items, sourceDisclaimer }: StatsProps) {
  return (
    <section
      aria-label="Agent statistics"
      style={{
        padding: "50px 40px",
        background: "white",
      }}
    >
      <dl style={{
        display: "flex",
        justifyContent: "center",
        gap: "20px",
        flexWrap: "wrap",
        maxWidth: "900px",
        margin: "0 auto",
      }}>
        {items.map((item) => (
          <div
            key={item.label}
            style={{
              background: "#FFF8F0",
              borderRadius: "16px",
              padding: "24px 32px",
              textAlign: "center",
              minWidth: "140px",
              boxShadow: "0 2px 8px rgba(0,0,0,0.06)",
            }}
          >
            <dd style={{
              fontSize: "28px",
              fontWeight: 700,
              color: "#4A3728",
              margin: 0,
            }}>
              {item.value}
            </dd>
            <dt style={{
              fontSize: "12px",
              textTransform: "uppercase",
              letterSpacing: "1px",
              marginTop: "4px",
              color: "#8B7355",
            }}>
              {item.label}
            </dt>
          </div>
        ))}
      </dl>
      {sourceDisclaimer && (
        <p style={{
          textAlign: "center",
          color: "#B0A090",
          fontSize: "11px",
          marginTop: "16px",
        }}>
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
