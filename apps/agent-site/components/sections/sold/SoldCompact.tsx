import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldCompact({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="gallery"
      style={{
        background: "#f5f5f5",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 800,
            color: "var(--color-primary, #1a1a1a)",
            marginBottom: "10px",
            fontFamily: "var(--font-family, Inter), sans-serif",
          }}
        >
          {title ?? "Recent Sales"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#666",
              fontSize: "16px",
              marginBottom: "45px",
            }}
          >
            {subtitle}
          </p>
        )}
        {!subtitle && <div style={{ marginBottom: "45px" }} />}

        <div
          data-sold-grid
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
            gap: "20px",
          }}
        >
          {items.map((item) => (
            <div
              key={`${item.address}-${item.city}`}
              style={{
                background: "white",
                borderRadius: "12px",
                overflow: "hidden",
                boxShadow: "0 2px 10px rgba(0,0,0,0.07)",
              }}
            >
              {item.image_url && (
                <div
                  style={{
                    position: "relative",
                    width: "100%",
                    aspectRatio: "4 / 3",
                  }}
                >
                  <Image
                    src={item.image_url}
                    alt={`${item.address}, ${item.city}`}
                    fill
                    style={{ objectFit: "cover" }}
                    sizes="(max-width: 768px) 100vw, 33vw"
                  />
                  {/* Price overlay */}
                  <div
                    style={{
                      position: "absolute",
                      bottom: 0,
                      left: 0,
                      right: 0,
                      background: "linear-gradient(transparent, rgba(0,0,0,0.7))",
                      padding: "20px 14px 10px",
                    }}
                  >
                    <span
                      style={{
                        color: "white",
                        fontSize: "18px",
                        fontWeight: 800,
                      }}
                    >
                      {item.price}
                    </span>
                  </div>
                </div>
              )}

              <div style={{ padding: "14px 16px" }}>
                {!item.image_url && (
                  <p
                    style={{
                      fontSize: "20px",
                      fontWeight: 800,
                      color: "var(--color-accent, #ff6b6b)",
                      marginBottom: "6px",
                    }}
                  >
                    {item.price}
                  </p>
                )}
                <p
                  style={{
                    fontSize: "14px",
                    fontWeight: 600,
                    color: "#1a1a1a",
                    marginBottom: "2px",
                  }}
                >
                  {item.address}
                </p>
                <p
                  style={{
                    fontSize: "13px",
                    color: "#777",
                  }}
                >
                  {item.city}, {item.state}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
