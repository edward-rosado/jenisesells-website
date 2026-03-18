import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldEstate({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="gallery"
      style={{
        background: "var(--color-bg, #faf6f0)",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "10px" : "48px",
            fontFamily: "Georgia, serif",
          }}
        >
          {title ?? "Properties Sold"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "var(--color-secondary, #8b6b3d)",
              fontSize: "16px",
              marginBottom: "48px",
              fontFamily: "sans-serif",
            }}
          >
            {subtitle}
          </p>
        )}

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
            gap: "28px",
          }}
        >
          {items.map((item) => (
            <article
              key={`${item.address}-${item.city}`}
              style={{
                background: "#f5f0e8",
                borderRadius: "10px",
                overflow: "hidden",
                position: "relative",
              }}
            >
              {/* Landscape image */}
              {item.image_url && (
                <div
                  style={{
                    position: "relative",
                    height: "220px",
                    overflow: "hidden",
                  }}
                >
                  <Image
                    src={item.image_url}
                    alt={`${item.address}, ${item.city}`}
                    fill
                    style={{ objectFit: "cover" }}
                    sizes="(max-width: 768px) 100vw, 350px"
                  />
                </div>
              )}

              {/* SOLD / badge_label badge */}
              <span
                style={{
                  position: "absolute",
                  top: "12px",
                  left: "12px",
                  background: "var(--color-accent, #4a6741)",
                  color: "white",
                  padding: "4px 14px",
                  borderRadius: "4px",
                  fontSize: "12px",
                  fontWeight: 700,
                  letterSpacing: "1px",
                  fontFamily: "sans-serif",
                }}
              >
                {item.badge_label ?? "SOLD"}
              </span>

              <div style={{ padding: "20px 22px" }}>
                {/* Serif price */}
                <div
                  aria-label={`Sold for ${item.price}`}
                  style={{
                    fontSize: "24px",
                    fontWeight: 700,
                    color: "var(--color-primary, #2d4a3e)",
                    fontFamily: "Georgia, serif",
                    marginBottom: "4px",
                  }}
                >
                  {item.price}
                </div>

                <div
                  style={{
                    fontSize: "13px",
                    color: "var(--color-secondary, #8b6b3d)",
                    marginBottom: item.features?.length ? "14px" : "0",
                    fontFamily: "sans-serif",
                  }}
                >
                  {item.address}, {item.city}, {item.state}
                </div>

                {/* Feature pills */}
                {item.features && item.features.length > 0 && (
                  <div
                    style={{
                      display: "flex",
                      flexWrap: "wrap",
                      gap: "8px",
                    }}
                  >
                    {item.features.map((feat) => (
                      <span
                        key={feat.label}
                        style={{
                          background: "var(--color-stone, #e8e2d8)",
                          border: "1px solid #cfc8bb",
                          borderRadius: "20px",
                          padding: "3px 12px",
                          fontSize: "12px",
                          color: "#5a5040",
                          fontFamily: "sans-serif",
                        }}
                      >
                        <strong>{feat.label}:</strong> {feat.value}
                      </span>
                    ))}
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
