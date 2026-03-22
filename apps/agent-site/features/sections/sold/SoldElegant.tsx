"use client";

import { useState } from "react";
import Image from "next/image";
import type { SoldHomesProps } from "@/features/sections/types";
import type { GalleryItem } from "@/features/config/types";

function SoldElegantCard({ item }: { item: GalleryItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      {item.image_url && (
        <div
          data-image-wrapper
          style={{
            position: "relative",
            aspectRatio: "16 / 9",
            overflow: "hidden",
            border: "1px solid var(--color-accent, #b8926a)",
            marginBottom: "14px",
          }}
        >
          <Image
            src={item.image_url}
            alt={`${item.address}, ${item.city}`}
            fill
            style={{ objectFit: "cover" }}
            sizes="480px"
          />
        </div>
      )}
      <div
        style={{
          fontSize: "22px",
          fontWeight: 300,
          color: "var(--color-primary, #3d3028)",
          fontFamily: "var(--font-family, Georgia), serif",
          marginBottom: "4px",
        }}
      >
        {item.price}
      </div>
      <div
        style={{
          fontSize: "13px",
          color: "var(--color-secondary, #5a4a3a)",
        }}
      >
        {item.address}, {item.city}, {item.state}
      </div>
    </div>
  );
}

export function SoldElegant({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="gallery"
      style={{
        background: "#f8f6f3",
        padding: "80px 40px",
      }}
    >
      <div style={{ maxWidth: "960px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "32px",
            fontWeight: 300,
            color: "var(--color-primary, #3d3028)",
            marginBottom: subtitle ? "10px" : "50px",
            fontFamily: "var(--font-family, Georgia), serif",
            letterSpacing: "1px",
          }}
        >
          {title ?? "Recent Sales"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "var(--color-secondary, #5a4a3a)",
              fontSize: "16px",
              marginBottom: "50px",
            }}
          >
            {subtitle}
          </p>
        )}

        <div
          data-sold-grid
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(2, 1fr)",
            gap: "32px",
          }}
        >
          {items.map((item) => (
            <SoldElegantCard key={`${item.address}-${item.city}`} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
