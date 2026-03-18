"use client";

import { useState } from "react";
import Image from "next/image";
import type { GalleryProps } from "@/components/sections/types";
import type { GalleryItem } from "@/lib/types";

function SoldGridCard({ item }: { item: GalleryItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      key={`${item.address}-${item.city}`}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#f9f9f9",
        borderRadius: "10px",
        padding: "20px",
        textAlign: "center",
        border: "1px solid #e0e0e0",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s, box-shadow 0.3s",
        cursor: "default",
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
          background: "var(--color-accent)",
          color: "var(--color-primary)",
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
        aria-label={`Sold for ${item.price}`}
        style={{
          fontSize: "22px",
          fontWeight: 800,
          color: "var(--color-primary)",
        }}
      >
        {item.price}
      </div>
      <div style={{ fontSize: "13px", color: "#666", marginTop: "5px" }}>
        {item.address}, {item.city}, {item.state}
      </div>
    </article>
  );
}

export function SoldGrid({ items, title, subtitle }: GalleryProps) {
  return (
    <section
      id="gallery"
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
          color: "var(--color-primary)",
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
          <SoldGridCard key={`${item.address}-${item.city}`} item={item} />
        ))}
      </div>
    </section>
  );
}
