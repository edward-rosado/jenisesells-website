import Image from "next/image";
import type { SoldHomeItem } from "@/lib/types";

interface SoldHomesProps {
  items: SoldHomeItem[];
  title?: string;
  subtitle?: string;
}

export function SoldHomes({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="sold"
      style={{
        padding: "70px 40px",
        maxWidth: "1100px",
        margin: "0 auto",
      }}
    >
      <h2
        style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#1B5E20",
          marginBottom: "10px",
        }}
      >
        {title ?? "Recently Sold"}
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
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: "20px",
        }}
      >
        {items.map((item) => (
          <div
            key={`${item.address}-${item.city}`}
            style={{
              background: "#f9f9f9",
              borderRadius: "10px",
              padding: "20px",
              textAlign: "center",
              border: "1px solid #e0e0e0",
            }}
          >
            {item.image_url && (
              <div
                style={{
                  width: "100%",
                  height: "180px",
                  position: "relative",
                  borderRadius: "8px 8px 0 0",
                  overflow: "hidden",
                  marginBottom: "10px",
                }}
              >
                <Image
                  src={item.image_url}
                  alt={`${item.address}, ${item.city}`}
                  fill
                  style={{ objectFit: "cover" }}
                  sizes="(max-width: 768px) 50vw, 220px"
                />
              </div>
            )}
            <span
              style={{
                display: "inline-block",
                background: "#C8A951",
                color: "#1B5E20",
                fontSize: "12px",
                fontWeight: 700,
                padding: "3px 10px",
                borderRadius: "12px",
                marginBottom: "10px",
              }}
            >
              SOLD
            </span>
            <div
              style={{
                fontSize: "22px",
                fontWeight: 800,
                color: "#1B5E20",
              }}
            >
              {item.price}
            </div>
            <div style={{ fontSize: "13px", color: "#666", marginTop: "5px" }}>
              {item.address}, {item.city}, {item.state}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
