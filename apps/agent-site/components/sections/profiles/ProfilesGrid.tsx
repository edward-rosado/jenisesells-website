import Image from "next/image";
import Link from "next/link";
import type { ProfilesProps } from "@/components/sections/types";

export function ProfilesGrid({ items, title, subtitle }: ProfilesProps) {
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
            <Link
              key={item.id}
              href={item.link ?? `/agents/${item.id}`}
              style={{ textDecoration: "none", color: "inherit" }}
            >
              <div
                style={{
                  background: "white",
                  borderRadius: "12px",
                  padding: "28px",
                  textAlign: "center",
                  boxShadow: "0 2px 8px rgba(0,0,0,0.08)",
                  transition: "box-shadow 0.2s",
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
          ))}
        </div>
      </div>
    </section>
  );
}
