import Image from "next/image";
import Link from "next/link";
import type { ProfilesProps } from "@/components/sections/types";

export function ProfilesCards({ items, title, subtitle, accountId }: ProfilesProps) {
  return (
    <section
      id="profiles"
      style={{
        padding: "70px 40px",
        background: "white",
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
          }}
        >
          {items.map((item) => (
            <Link
              key={item.id}
              href={item.link ?? `/agents/${item.id}${accountId ? `?accountId=${encodeURIComponent(accountId)}` : ""}`}
              style={{ textDecoration: "none", color: "inherit" }}
            >
              <div
                style={{
                  background: "color-mix(in srgb, var(--color-primary) 5%, white)",
                  borderRadius: "16px",
                  padding: "32px 28px",
                  textAlign: "center",
                  boxShadow: "0 4px 16px rgba(0,0,0,0.08)",
                  transition: "box-shadow 0.2s, transform 0.2s",
                }}
              >
                <div
                  style={{
                    width: "140px",
                    height: "140px",
                    borderRadius: "50%",
                    overflow: "hidden",
                    margin: "0 auto 20px",
                    background: "#e8e8e8",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    flexShrink: 0,
                    position: "relative",
                    border: "3px solid var(--color-accent, #C8A951)",
                  }}
                >
                  {item.headshot_url ? (
                    <Image
                      src={item.headshot_url}
                      alt={item.name}
                      fill
                      style={{ objectFit: "cover" }}
                      sizes="140px"
                    />
                  ) : (
                    <span
                      style={{
                        fontSize: "48px",
                        fontWeight: 700,
                        color: "#999",
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
                    marginBottom: "6px",
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
          ))}
        </div>
      </div>
    </section>
  );
}
