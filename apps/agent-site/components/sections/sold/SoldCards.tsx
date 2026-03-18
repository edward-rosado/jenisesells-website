import Image from "next/image";
import type { GalleryProps } from "@/components/sections/types";

export function SoldCards({ items, title, subtitle }: GalleryProps) {
  return (
    <section
      id="sold"
      style={{
        padding: "70px 40px",
        background: "white",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: subtitle ? "8px" : "40px",
        }}>
          {title ?? "Recently Sold"}
        </h2>
        {subtitle && (
          <p style={{
            textAlign: "center",
            color: "#8B7355",
            fontSize: "16px",
            marginBottom: "40px",
          }}>
            {subtitle}
          </p>
        )}
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "24px",
        }}>
          {items.map((item) => (
            <article
              key={`${item.address}-${item.city}`}
              style={{
                background: "#FFF8F0",
                borderRadius: "16px",
                overflow: "hidden",
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
                position: "relative",
              }}
            >
              {item.image_url && (
                <div style={{
                  position: "relative",
                  height: "200px",
                  borderRadius: "16px 16px 0 0",
                  overflow: "hidden",
                }}>
                  <Image
                    src={item.image_url}
                    alt={`${item.address}, ${item.city}`}
                    fill
                    style={{ objectFit: "cover" }}
                    sizes="(max-width: 768px) 100vw, 280px"
                  />
                </div>
              )}
              <span style={{
                position: "absolute",
                top: "12px",
                left: "12px",
                background: "var(--color-accent)",
                color: "white",
                padding: "4px 12px",
                borderRadius: "20px",
                fontSize: "12px",
                fontWeight: 700,
              }}>
                SOLD
              </span>
              <div style={{ padding: "20px" }}>
                <div style={{
                  fontSize: "22px",
                  fontWeight: 700,
                  color: "#4A3728",
                  marginBottom: "4px",
                }}>
                  {item.price}
                </div>
                <div style={{
                  fontSize: "14px",
                  color: "#8B7355",
                }}>
                  {item.address}, {item.city}, {item.state}
                </div>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
