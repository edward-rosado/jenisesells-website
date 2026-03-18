"use client";

import { useState } from "react";
import Image from "next/image";
import type { GalleryProps } from "@/components/sections/types";
import type { GalleryItem } from "@/lib/types";

function SoldMinimalCard({ item }: { item: GalleryItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        borderRadius: "8px",
        overflow: "hidden",
        border: "1px solid #eee",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
      }}
    >
      {item.image_url && (
        <div style={{
          width: "100%",
          height: "160px",
          position: "relative",
        }}>
          <Image
            src={item.image_url}
            alt={`${item.address}, ${item.city}`}
            fill
            style={{ objectFit: "cover" }}
            sizes="(max-width: 768px) 50vw, 220px"
          />
        </div>
      )}
      <div style={{ padding: "16px" }}>
        <span style={{
          display: "inline-block",
          background: "#e8e8e8",
          color: "#555",
          fontSize: "11px",
          fontWeight: 600,
          padding: "2px 8px",
          borderRadius: "4px",
          marginBottom: "6px",
          letterSpacing: "0.5px",
        }}>
          SOLD
        </span>
        <div style={{
          fontSize: "20px",
          fontWeight: 700,
          color: "#1a1a1a",
          marginBottom: "4px",
        }}>
          {item.price}
        </div>
        <div style={{
          fontSize: "13px",
          color: "#888",
        }}>
          {item.address}, {item.city}, {item.state}
        </div>
      </div>
    </article>
  );
}

export function SoldMinimal({ items, title, subtitle }: GalleryProps) {
  return (
    <section
      id="gallery"
      style={{
        padding: "80px 40px",
        maxWidth: "1000px",
        margin: "0 auto",
      }}
    >
      <h2 style={{
        textAlign: "center",
        fontSize: "32px",
        fontWeight: 600,
        color: "#1a1a1a",
        marginBottom: "8px",
        letterSpacing: "-0.3px",
      }}>
        {title ?? "Recently Sold"}
      </h2>
      {subtitle && (
        <p style={{
          textAlign: "center",
          color: "#888",
          fontSize: "16px",
          marginBottom: "50px",
        }}>
          {subtitle}
        </p>
      )}
      <div style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
        gap: "24px",
      }}>
        {items.map((item) => (
          <SoldMinimalCard key={`${item.address}-${item.city}`} item={item} />
        ))}
      </div>
    </section>
  );
}
