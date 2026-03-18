import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";

export function SoldCoastal({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="gallery"
      style={{
        padding: "70px 40px",
        background: "var(--color-bg, #e8f4f8)",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 700,
            color: "var(--color-primary, #2c7a7b)",
            marginBottom: subtitle ? "8px" : "40px",
          }}
        >
          {title ?? "Recent Sales"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#4a6c6c",
              fontSize: "16px",
              marginBottom: "40px",
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: "24px",
          }}
        >
          {items.map((item) => (
            <article
              key={`${item.address}-${item.city}`}
              style={{
                background: "#fefcf8",
                borderRadius: "14px",
                overflow: "hidden",
                boxShadow: "0 2px 12px rgba(44, 122, 123, 0.1)",
                position: "relative",
              }}
            >
              {item.image_url && (
                <div
                  style={{
                    position: "relative",
                    height: "200px",
                    overflow: "hidden",
                  }}
                >
                  <Image
                    src={item.image_url}
                    alt={`${item.address}, ${item.city}`}
                    fill
                    style={{ objectFit: "cover" }}
                    sizes="(max-width: 768px) 100vw, 280px"
                  />
                </div>
              )}
              {/* SOLD badge */}
              <span
                style={{
                  position: "absolute",
                  top: "12px",
                  left: "12px",
                  background: "var(--color-primary, #2c7a7b)",
                  color: "white",
                  padding: "4px 12px",
                  borderRadius: "20px",
                  fontSize: "12px",
                  fontWeight: 700,
                }}
              >
                {item.badge_label ?? "SOLD"}
              </span>
              <div style={{ padding: "18px 20px" }}>
                {/* Tags as colored pills */}
                {item.tags && item.tags.length > 0 && (
                  <div
                    style={{
                      display: "flex",
                      flexWrap: "wrap",
                      gap: "6px",
                      marginBottom: "10px",
                    }}
                  >
                    {item.tags.map((tag) => (
                      <span
                        key={tag}
                        style={{
                          background: "var(--color-primary, #2c7a7b)",
                          color: "white",
                          padding: "3px 10px",
                          borderRadius: "12px",
                          fontSize: "11px",
                          fontWeight: 600,
                        }}
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                )}
                <div
                  style={{
                    fontSize: "22px",
                    fontWeight: 700,
                    color: "var(--color-primary, #2c7a7b)",
                    marginBottom: "4px",
                  }}
                >
                  {item.price}
                </div>
                <div
                  style={{
                    fontSize: "14px",
                    color: "#4a6c6c",
                  }}
                >
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
