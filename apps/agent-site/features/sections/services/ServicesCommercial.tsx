"use client";

import { useState } from "react";
import type { ServicesProps } from "@/features/sections/types";
import type { ServiceItem } from "@/features/config/types";

function groupByCategory(items: ServiceItem[]): Map<string | undefined, ServiceItem[]> {
  const map = new Map<string | undefined, ServiceItem[]>();
  for (const item of items) {
    const key = item.category;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(item);
  }
  return map;
}

function ServicesCommercialCard({ item, headingLevel }: { item: ServiceItem; headingLevel: "h3" | "h4" }) {
  const [hover, setHover] = useState(false);
  const Heading = headingLevel;
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#f8fafc",
        border: "1px solid #e2e8f0",
        borderRadius: "6px",
        padding: "24px",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      <Heading
        style={{
          fontSize: "16px",
          fontWeight: 700,
          color: "#0f172a",
          marginBottom: "8px",
        }}
      >
        {item.title}
      </Heading>
      <p style={{ color: "#64748b", fontSize: "14px", lineHeight: 1.6 }}>
        {item.description}
      </p>
    </article>
  );
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
                    <ServicesCommercialCard key={item.title} item={item} headingLevel="h4" />
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
              <ServicesCommercialCard key={item.title} item={item} headingLevel="h3" />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
