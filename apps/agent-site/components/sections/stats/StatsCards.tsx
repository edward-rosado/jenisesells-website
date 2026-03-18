import type { StatsProps } from "@/components/sections/types";

export function StatsCards({ items, sourceDisclaimer }: StatsProps) {
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
        maxWidth: "800px",
        margin: "0 auto",
      }}>
        {items.map((item) => (
          <div
            key={item.label}
            style={{
              display: "flex",
              flexDirection: "column",
              border: "1px solid #eee",
              borderRadius: "12px",
              padding: "24px 32px",
              textAlign: "center",
              minWidth: "140px",
            }}
          >
            <dt style={{
              order: 2,
              fontSize: "12px",
              textTransform: "uppercase",
              letterSpacing: "1px",
              marginTop: "4px",
              color: "#767676",
            }}>
              {item.label}
            </dt>
            <dd style={{
              order: 1,
              fontSize: "28px",
              fontWeight: 700,
              color: "#1a1a1a",
              margin: 0,
            }}>
              {item.value}
            </dd>
          </div>
        ))}
      </dl>
      {sourceDisclaimer && (
        <p style={{
          textAlign: "center",
          color: "#767676",
          fontSize: "11px",
          marginTop: "16px",
        }}>
          {sourceDisclaimer}
        </p>
      )}
    </section>
  );
}
