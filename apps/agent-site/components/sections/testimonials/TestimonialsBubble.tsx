"use client";

import { useState } from "react";
import { clampRating, FTC_DISCLAIMER, type TestimonialsProps } from "@/components/sections/types";
import type { TestimonialItem } from "@/lib/types";

function BubbleCard({ item }: { item: TestimonialItem }) {
  const [hover, setHover] = useState(false);

  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        transform: hover ? "translateY(-4px)" : "none",
        transition: "transform 0.3s",
      }}
    >
      {/* Speech bubble */}
      <article style={{
        background: "white",
        borderRadius: "20px",
        padding: "24px",
        position: "relative",
        boxShadow: hover
          ? "0 6px 20px rgba(0,0,0,0.12)"
          : "0 2px 12px rgba(0,0,0,0.06)",
        marginBottom: "16px",
        transition: "box-shadow 0.3s",
      }}>
        <span
          role="img"
          aria-label={`${clampRating(item.rating)} out of 5 stars`}
          style={{
            display: "block",
            color: "var(--color-accent)",
            fontSize: "16px",
            marginBottom: "10px",
          }}
        >
          {"★".repeat(clampRating(item.rating))}{"☆".repeat(5 - clampRating(item.rating))}
        </span>
        <p style={{
          fontStyle: "italic",
          color: "#6B5A4A",
          fontSize: "14px",
          lineHeight: 1.7,
          margin: 0,
        }}>
          {item.text}
        </p>
        {/* Bubble tail */}
        <div style={{
          position: "absolute",
          bottom: "-8px",
          left: "24px",
          width: "16px",
          height: "16px",
          background: "white",
          transform: "rotate(45deg)",
          boxShadow: "2px 2px 4px rgba(0,0,0,0.04)",
        }} />
      </article>
      {/* Reviewer info below bubble */}
      <div style={{
        display: "flex",
        alignItems: "center",
        gap: "12px",
        paddingLeft: "12px",
      }}>
        <div aria-hidden="true" style={{
          width: "36px",
          height: "36px",
          borderRadius: "50%",
          background: "var(--color-accent)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          color: "white",
          fontWeight: 700,
          fontSize: "14px",
        }}>
          {item.reviewer.charAt(0)}
        </div>
        <div>
          <div style={{
            fontWeight: 700,
            color: "#4A3728",
            fontSize: "14px",
          }}>
            {item.reviewer}
          </div>
          {item.source && (
            <div style={{ color: "#8B7355", fontSize: "12px" }}>
              via {item.source}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export function TestimonialsBubble({ items, title }: TestimonialsProps) {
  return (
    <section
      id="testimonials"
      style={{
        background: "#FFF8F0",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2 style={{
          textAlign: "center",
          fontSize: "32px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: "8px",
        }}>
          {title ?? "What My Clients Say"}
        </h2>
        <p style={{
          textAlign: "center",
          color: "#8B7355",
          fontSize: "12px",
          marginBottom: "45px",
        }}>
          {FTC_DISCLAIMER}
        </p>
        <div style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "28px",
        }}>
          {items.map((item) => (
            <BubbleCard key={item.reviewer} item={item} />
          ))}
        </div>
      </div>
    </section>
  );
}
