import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";

export function AboutCompact({ agent, data }: AboutProps) {
  return (
    <section
      id="about"
      style={{
        background: "#fafafa",
        padding: "70px 40px",
      }}
    >
      <div style={{ maxWidth: "900px", margin: "0 auto" }}>
        <h2
          style={{
            fontSize: "34px",
            fontWeight: 800,
            color: "var(--color-primary, #1a1a1a)",
            marginBottom: "40px",
            fontFamily: "var(--font-family, Inter), sans-serif",
          }}
        >
          {data.title ?? `About ${agent.identity.name}`}
        </h2>

        <div
          data-about-layout
          style={{
            display: "flex",
            gap: "32px",
            alignItems: "flex-start",
            flexWrap: "wrap",
          }}
        >
          {/* Agent photo */}
          {agent.identity.headshot_url && (
            <div
              data-photo-wrapper
              style={{
                width: "120px",
                height: "120px",
                borderRadius: "50%",
                overflow: "hidden",
                position: "relative",
                flexShrink: 0,
                border: "3px solid var(--color-accent, #ff6b6b)",
              }}
            >
              <Image
                src={agent.identity.headshot_url}
                alt={agent.identity.name}
                fill
                style={{ objectFit: "cover" }}
                sizes="120px"
              />
            </div>
          )}

          <div style={{ flex: 1, minWidth: "200px" }}>
            {/* Name + title inline */}
            <div style={{ marginBottom: "16px" }}>
              <span
                style={{
                  fontSize: "20px",
                  fontWeight: 800,
                  color: "var(--color-primary, #1a1a1a)",
                  fontFamily: "var(--font-family, Inter), sans-serif",
                  marginRight: "10px",
                }}
              >
                {agent.identity.name}
              </span>
              {agent.identity.title && (
                <span
                  style={{
                    fontSize: "14px",
                    color: "var(--color-accent, #ff6b6b)",
                    fontWeight: 600,
                  }}
                >
                  {agent.identity.title}
                </span>
              )}
            </div>

            {/* Bio */}
            {Array.isArray(data.bio) ? (
              data.bio.map((paragraph, i) => (
                <p
                  key={i}
                  style={{
                    fontSize: "15px",
                    color: "#555",
                    lineHeight: 1.7,
                    marginBottom: "12px",
                  }}
                >
                  {paragraph}
                </p>
              ))
            ) : (
              <p
                style={{
                  fontSize: "15px",
                  color: "#555",
                  lineHeight: 1.7,
                  marginBottom: "20px",
                }}
              >
                {data.bio}
              </p>
            )}

            {/* Credential badges */}
            {data.credentials && data.credentials.length > 0 && (
              <ul
                aria-label="Credentials"
                style={{
                  display: "flex",
                  flexWrap: "wrap",
                  gap: "8px",
                  marginTop: "16px",
                  listStyle: "none",
                  padding: 0,
                }}
              >
                {data.credentials.map((cred) => (
                  <li
                    key={cred}
                    style={{
                      border: "2px solid var(--color-accent, #ff6b6b)",
                      color: "var(--color-accent, #ff6b6b)",
                      padding: "4px 14px",
                      borderRadius: "20px",
                      fontSize: "12px",
                      fontWeight: 700,
                      letterSpacing: "0.5px",
                    }}
                  >
                    {cred}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
