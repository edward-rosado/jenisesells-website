"use client";

import { useState } from "react";
import Image from "next/image";
import type { SoldHomesProps } from "@/components/sections/types";
import type { GalleryItem } from "@/lib/types";

function StoryCard({ item }: { item: GalleryItem }) {
  const [hover, setHover] = useState(false);
  return (
    <article
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#f0f7f4",
        borderRadius: "16px",
        overflow: "hidden",
        boxShadow: hover
          ? "0 8px 28px rgba(90,158,124,0.22)"
          : "0 4px 16px rgba(90,158,124,0.1)",
        position: "relative",
        transition: "transform 0.3s, box-shadow 0.3s",
        transform: hover ? "translateY(-4px)" : "none",
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
            style={{
              objectFit: "cover",
              transition: "transform 0.3s",
              transform: hover ? "scale(1.05)" : "none",
            }}
            sizes="(max-width: 768px) 100vw, 280px"
          />
        </div>
      )}
      <div style={{ padding: "24px" }}>
        <div
          style={{
            fontSize: "24px",
            fontWeight: 700,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: "4px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {item.price}
        </div>
        <div
          style={{
            fontSize: "14px",
            color: "#4a6b5a",
            marginBottom: item.client_quote ? "16px" : "0",
          }}
        >
          {item.address}, {item.city}, {item.state}
        </div>
        {item.client_quote && (
          <blockquote
            style={{
              margin: 0,
              padding: "12px 16px",
              background: "white",
              borderRadius: "10px",
              borderLeft: "3px solid var(--color-accent, #5a9e7c)",
            }}
          >
            <p
              style={{
                fontSize: "13px",
                color: "#4a6b5a",
                fontStyle: "italic",
                lineHeight: 1.6,
                margin: 0,
                marginBottom: item.client_name ? "8px" : "0",
              }}
            >
              {item.client_quote}
            </p>
            {item.client_name && (
              <footer
                style={{
                  fontSize: "12px",
                  fontWeight: 600,
                  color: "var(--color-accent, #5a9e7c)",
                }}
              >
                — {item.client_name}
              </footer>
            )}
          </blockquote>
        )}
      </div>
    </article>
  );
}

export function SoldStories({ items, title, subtitle }: SoldHomesProps) {
  return (
    <section
      id="gallery"
      style={{
        background: "white",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        <h2
          style={{
            textAlign: "center",
            fontSize: "34px",
            fontWeight: 600,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: subtitle ? "8px" : "48px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {title ?? "Happy Homeowners"}
        </h2>
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#4a6b5a",
              fontSize: "16px",
              marginBottom: "48px",
              lineHeight: 1.6,
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
            <StoryCard key={`${item.address}-${item.city}`} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
