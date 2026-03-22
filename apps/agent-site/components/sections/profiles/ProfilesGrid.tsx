"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import type { ProfilesProps } from "@/components/sections/types";
import type { ProfileItem } from "@/features/config/types";

function ProfilesGridCard({ item, accountId }: { item: ProfileItem; accountId?: string }) {
  const [hover, setHover] = useState(false);
  return (
    <Link
      href={item.link ?? `/agents/${item.id}${accountId ? `?accountId=${encodeURIComponent(accountId)}` : ""}`}
      style={{ textDecoration: "none", color: "inherit" }}
    >
      <div
        onMouseEnter={() => setHover(true)}
        onMouseLeave={() => setHover(false)}
        style={{
          background: "white",
          borderRadius: "12px",
          padding: "28px",
          textAlign: "center",
          boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.08)",
          transform: hover ? "translateY(-4px)" : "none",
          transition: "transform 0.3s, box-shadow 0.3s",
          cursor: "default",
        }}
      >
        <div
          style={{
            width: "120px",
            height: "120px",
            borderRadius: "50%",
            overflow: "hidden",
            margin: "0 auto 16px",
            background: "#e0e0e0",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexShrink: 0,
            position: "relative",
          }}
        >
          {item.headshot_url ? (
            <Image
              src={item.headshot_url}
              alt={item.name}
              fill
              style={{ objectFit: "cover" }}
              sizes="120px"
            />
          ) : (
            <span
              style={{
                fontSize: "40px",
                fontWeight: 700,
                color: "#767676",
                lineHeight: 1,
              }}
            >
              {item.name.charAt(0)}
            </span>
          )}
        </div>
        <p
          style={{
            fontSize: "18px",
            fontWeight: 700,
            color: "var(--color-primary)",
            marginBottom: "4px",
          }}
        >
          {item.name}
        </p>
        <p
          style={{
            fontSize: "14px",
            color: "#666",
          }}
        >
          {item.title}
        </p>
      </div>
    </Link>
  );
}

export function ProfilesGrid({ items, title, subtitle, accountId }: ProfilesProps) {
  return (
    <section
      id="profiles"
      style={{
        padding: "70px 40px",
        background: "#f5f5f5",
        maxWidth: "100%",
      }}
    >
      <div style={{ maxWidth: "1100px", margin: "0 auto" }}>
        {title && (
          <h2
            style={{
              textAlign: "center",
              fontSize: "32px",
              fontWeight: 700,
              color: "var(--color-primary)",
              marginBottom: "10px",
            }}
          >
            {title}
          </h2>
        )}
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#666",
              fontSize: "15px",
              marginBottom: "45px",
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
            gap: "25px",
            marginTop: title || subtitle ? "0" : "0",
          }}
        >
          {items.map((item) => (
            <ProfilesGridCard key={item.id} item={item} accountId={accountId} />
          ))}
        </div>
      </div>
    </section>
  );
}
