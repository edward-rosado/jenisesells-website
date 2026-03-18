import type { ServicesProps } from "@/components/sections/types";
import type { ServiceItem } from "@/lib/types";

function groupByCategory(items: ServiceItem[]): Map<string | undefined, ServiceItem[]> {
  const map = new Map<string | undefined, ServiceItem[]>();
  for (const item of items) {
    const key = item.category;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(item);
  }
  return map;
}

export function ServicesCommercial({ items, title, subtitle }: ServicesProps) {
  const hasCategorized = items.some((i) => i.category);
  const grouped = hasCategorized ? groupByCategory(items) : null;

  return (
    <section
      id="features"
      style={{
        background: "white",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "#0f172a",
            marginBottom: subtitle ? "8px" : "48px",
            letterSpacing: "-0.3px",
          }}
        >
          {title ?? "Our Services"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#64748b",
              fontSize: "16px",
              marginBottom: "48px",
            }}
          >
            {subtitle}
          </p>
        )}

        {grouped ? (
          // Two-tier layout: category heading + service cards within
          <div style={{ display: "flex", flexDirection: "column", gap: "48px" }}>
            {Array.from(grouped.entries()).map(([category, categoryItems]) => (
              <div key={category ?? "uncategorized"}>
                {category && (
                  <h3
                    style={{
                      fontSize: "13px",
                      fontWeight: 700,
                      textTransform: "uppercase",
                      letterSpacing: "1.2px",
                      color: "#2563eb",
                      marginBottom: "20px",
                      paddingBottom: "8px",
                      borderBottom: "2px solid #e2e8f0",
                    }}
                  >
                    {category}
                  </h3>
                )}
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
                    gap: "20px",
                  }}
                >
                  {categoryItems.map((item) => (
                    <article
                      key={item.title}
                      style={{
                        background: "#f8fafc",
                        border: "1px solid #e2e8f0",
                        borderRadius: "6px",
                        padding: "24px",
                      }}
                    >
                      <h4
                        style={{
                          fontSize: "16px",
                          fontWeight: 700,
                          color: "#0f172a",
                          marginBottom: "8px",
                        }}
                      >
                        {item.title}
                      </h4>
                      <p style={{ color: "#64748b", fontSize: "14px", lineHeight: 1.6 }}>
                        {item.description}
                      </p>
                    </article>
                  ))}
                </div>
              </div>
            ))}
          </div>
        ) : (
          // Flat grid layout
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
              gap: "20px",
            }}
          >
            {items.map((item) => (
              <article
                key={item.title}
                style={{
                  background: "#f8fafc",
                  border: "1px solid #e2e8f0",
                  borderRadius: "6px",
                  padding: "24px",
                }}
              >
                <h3
                  style={{
                    fontSize: "16px",
                    fontWeight: 700,
                    color: "#0f172a",
                    marginBottom: "8px",
                  }}
                >
                  {item.title}
                </h3>
                <p style={{ color: "#64748b", fontSize: "14px", lineHeight: 1.6 }}>
                  {item.description}
                </p>
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
