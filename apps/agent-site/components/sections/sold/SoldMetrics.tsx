import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldMetrics({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="sold"
      style={{
        background: "#f4f5f7",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "1200px", margin: "0 auto" }}>
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
          {title ?? "Recent Transactions"}
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

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <article
              key={`${item.address}-${item.city}`}
              style={{
                background: "white",
                borderRadius: "6px",
                overflow: "hidden",
                border: "1px solid #e2e8f0",
                boxShadow: "0 1px 4px rgba(0,0,0,0.05)",
                position: "relative",
              }}
            >
              {/* Property image */}
              {item.image_url && (
                <div style={{ position: "relative", height: "180px", overflow: "hidden" }}>
                  <Image
                    src={item.image_url}
                    alt={`${item.address}, ${item.city}`}
                    fill
                    style={{ objectFit: "cover" }}
                    sizes="(max-width: 768px) 100vw, 300px"
                  />
                </div>
              )}

              {/* Badges row: closed + property type */}
              <div
                style={{
                  display: "flex",
                  gap: "8px",
                  padding: "14px 16px 0",
                  flexWrap: "wrap",
                }}
              >
                {/* CLOSED badge */}
                <span
                  style={{
                    background: "#1e293b",
                    color: "white",
                    padding: "3px 10px",
                    borderRadius: "3px",
                    fontSize: "11px",
                    fontWeight: 700,
                    letterSpacing: "0.8px",
                    textTransform: "uppercase",
                  }}
                >
                  {item.badge_label ?? "CLOSED"}
                </span>

                {/* Property type badge */}
                {item.property_type && (
                  <span
                    style={{
                      background: "#eff6ff",
                      color: "#2563eb",
                      padding: "3px 10px",
                      borderRadius: "3px",
                      fontSize: "11px",
                      fontWeight: 700,
                      letterSpacing: "0.5px",
                    }}
                  >
                    {item.property_type}
                  </span>
                )}
              </div>

              <div style={{ padding: "12px 16px 20px" }}>
                {/* Price */}
                <div
                  style={{
                    fontSize: "24px",
                    fontWeight: 800,
                    color: "#0f172a",
                    marginBottom: "4px",
                  }}
                >
                  {item.price}
                </div>

                {/* Address */}
                <div
                  style={{
                    fontSize: "13px",
                    color: "#64748b",
                    marginBottom: "14px",
                  }}
                >
                  {item.address}, {item.city}, {item.state}
                </div>

                {/* Metrics grid: sq_ft, cap_rate, noi */}
                {(item.sq_ft || item.cap_rate || item.noi) && (
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "repeat(auto-fit, minmax(80px, 1fr))",
                      gap: "8px",
                      borderTop: "1px solid #f1f5f9",
                      paddingTop: "12px",
                    }}
                  >
                    {item.sq_ft && (
                      <div>
                        <div
                          style={{
                            fontSize: "15px",
                            fontWeight: 700,
                            color: "#1e293b",
                          }}
                        >
                          {item.sq_ft}
                        </div>
                        <div
                          style={{
                            fontSize: "11px",
                            color: "#94a3b8",
                            textTransform: "uppercase",
                            letterSpacing: "0.5px",
                          }}
                        >
                          Size
                        </div>
                      </div>
                    )}
                    {item.cap_rate && (
                      <div>
                        <div
                          style={{
                            fontSize: "15px",
                            fontWeight: 700,
                            color: "#1e293b",
                          }}
                        >
                          {item.cap_rate}
                        </div>
                        <div
                          style={{
                            fontSize: "11px",
                            color: "#94a3b8",
                            textTransform: "uppercase",
                            letterSpacing: "0.5px",
                          }}
                        >
                          Cap Rate
                        </div>
                      </div>
                    )}
                    {item.noi && (
                      <div>
                        <div
                          style={{
                            fontSize: "15px",
                            fontWeight: 700,
                            color: "#1e293b",
                          }}
                        >
                          {item.noi}
                        </div>
                        <div
                          style={{
                            fontSize: "11px",
                            color: "#94a3b8",
                            textTransform: "uppercase",
                            letterSpacing: "0.5px",
                          }}
                        >
                          NOI
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
