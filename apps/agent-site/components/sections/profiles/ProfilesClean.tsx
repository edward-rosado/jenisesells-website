import Image from "next/image";
import Link from "next/link";
import type { ProfilesProps } from "@/components/sections/types";

export function ProfilesClean({ items, title, subtitle }: ProfilesProps) {
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
              color: "#aaa",
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
            <Link
              key={item.id}
              href={item.link ?? `/agents/${item.id}`}
              style={{ textDecoration: "none", color: "inherit" }}
            >
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "20px",
                  padding: "20px 0",
                  borderTop: index === 0 ? "1px solid #eee" : undefined,
                  borderBottom: "1px solid #eee",
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
                        color: "#999",
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
                      color: "#888",
                    }}
                  >
                    {item.title}
                  </p>
                </div>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
