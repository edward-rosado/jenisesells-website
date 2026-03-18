"use client";

import { useState } from "react";
import type { FeaturesProps } from "@/components/sections/types";
import type { ServiceItem } from "@/lib/types";

function ServicesGridCard({ item }: { item: ServiceItem }) {
  const [hover, setHover] = useState(false);
  return (
    <div
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        background: "#f9f9f9",
        borderRadius: "12px",
        padding: "30px",
        borderLeft: "4px solid var(--color-secondary)",
        transition: "transform 0.3s, box-shadow 0.3s",
        transform: hover ? "translateY(-4px)" : "none",
        boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
        cursor: "default",
      }}
    >
      <h3
        style={{
          color: "var(--color-primary)",
          fontSize: "19px",
          marginBottom: "10px",
        }}
      >
        {item.title}
      </h3>
      <p style={{ color: "#555", fontSize: "15px" }}>{item.description}</p>
    </div>
  );
}

export function ServicesGrid({ items, title, subtitle }: FeaturesProps) {
  return (
    <section
      id="features"
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
        {title ?? "What I Do for You"}
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
          gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
          gap: "25px",
        }}
      >
        {items.map((item) => (
          <ServicesGridCard key={item.title} item={item} />
        ))}
      </div>
    </section>
  );
}
