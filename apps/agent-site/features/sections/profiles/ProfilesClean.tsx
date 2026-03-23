"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import type { ProfilesProps } from "@/features/sections/types";
import type { ProfileItem } from "@/features/config/types";

function ProfilesCleanRow({ item, isFirst, accountId }: { item: ProfileItem; isFirst: boolean; accountId?: string }) {
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
          display: "flex",
          alignItems: "center",
          gap: "20px",
          padding: "20px 0",
          borderTop: isFirst ? "1px solid #eee" : undefined,
          borderBottom: "1px solid #eee",
          boxShadow: hover ? "0 6px 20px rgba(0,0,0,0.12)" : "0 2px 8px rgba(0,0,0,0.06)",
          transform: hover ? "translateY(-4px)" : "none",
          transition: "transform 0.3s, box-shadow 0.3s",
          cursor: "default",
          borderRadius: "8px",
          paddingLeft: "12px",
          paddingRight: "12px",
        }}
      >
        <div
          style={{
            width: "80px",
            height: "80px",
            borderRadius: "50%",
            overflow: "hidden",
            background: "#e8e8e8",
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
              sizes="80px"
            />
          ) : (
            <span
              style={{
                fontSize: "28px",
                fontWeight: 700,
                color: "#767676",
                lineHeight: 1,
              }}
            >
              {item.name.charAt(0)}
            </span>
          )}
        </div>
        <div>
          <p
            style={{
              fontSize: "16px",
              fontWeight: 600,
              color: "#1a1a1a",
              marginBottom: "4px",
            }}
          >
            {item.name}
          </p>
          <p
            style={{
              fontSize: "14px",
              color: "#555",
            }}
          >
            {item.title}
          </p>
        </div>
      </div>
    </Link>
  );
}

export function ProfilesClean({ items, title, subtitle, accountId }: ProfilesProps) {
  return (
    <section
      id="profiles"
      style={{
        padding: "80px 40px",
        background: "#fafafa",
        maxWidth: "100%",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        {title && (
          <h2
            style={{
              textAlign: "center",
              fontSize: "32px",
              fontWeight: 600,
              color: "#1a1a1a",
              marginBottom: "8px",
              letterSpacing: "-0.3px",
            }}
          >
            {title}
          </h2>
        )}
        {subtitle && (
          <p
            style={{
              textAlign: "center",
              color: "#767676",
              fontSize: "14px",
              marginBottom: "50px",
            }}
          >
            {subtitle}
          </p>
        )}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
          }}
        >
          {items.map((item, index) => (
            <ProfilesCleanRow key={item.id} item={item} isFirst={index === 0} accountId={accountId} />
          ))}
        </div>
      </div>
    </section>
  );
}
