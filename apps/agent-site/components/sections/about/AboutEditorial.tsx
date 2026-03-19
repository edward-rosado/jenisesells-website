import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/components/sections/types";

export function AboutEditorial({ agent, data }: AboutProps) {
  const displayName = getDisplayName(agent);
  const headshotUrl = getHeadshotUrl(agent);

  return (
    <section
      id="about"
      style={{
        background: "var(--color-primary, #0a0a0a)",
        padding: "80px 40px",
      }}
    >
      <div
        style={{
          maxWidth: "900px",
          margin: "0 auto",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          gap: "40px",
        }}
      >
        {/* Agent photo */}
        {headshotUrl && (
          <div
            data-photo-wrapper
            style={{
              width: "220px",
              height: "220px",
              borderRadius: "50%",
              border: "2px solid var(--color-accent, #d4af37)",
              overflow: "hidden",
              position: "relative",
              flexShrink: 0,
            }}
          >
            <Image
              src={headshotUrl}
              alt={`Photo of ${displayName}`}
              fill
              style={{ objectFit: "cover" }}
              sizes="220px"
            />
          </div>
        )}

        <div style={{ maxWidth: "700px", textAlign: "center" }}>
          <h2
            style={{
              fontSize: "34px",
              fontWeight: 300,
              color: "white",
              marginBottom: "30px",
              fontFamily: "var(--font-family, Georgia), serif",
              letterSpacing: "1px",
            }}
          >
            {data.title ?? `About ${displayName}`}
          </h2>

          {/* Bio */}
          {Array.isArray(data.bio) ? (
            data.bio.map((paragraph, i) => (
              <p
                key={i}
                style={{
                  fontSize: "16px",
                  color: "rgba(255,255,255,0.75)",
                  lineHeight: 1.8,
                  marginBottom: "16px",
                  fontFamily: "var(--font-family, Georgia), serif",
                  fontWeight: 300,
                }}
              >
                {paragraph}
              </p>
            ))
          ) : (
            <p
              style={{
                fontSize: "16px",
                color: "rgba(255,255,255,0.75)",
                lineHeight: 1.8,
                marginBottom: "16px",
                fontFamily: "var(--font-family, Georgia), serif",
                fontWeight: 300,
              }}
            >
              {data.bio}
            </p>
          )}

          {/* Credentials */}
          {data.credentials && data.credentials.length > 0 && (
            <ul
              aria-label="Credentials"
              style={{
                display: "flex",
                flexWrap: "wrap",
                gap: "10px",
                marginTop: "24px",
                listStyle: "none",
                padding: 0,
                justifyContent: "center",
              }}
            >
              {data.credentials.map((cred) => (
                <li
                  key={cred}
                  style={{
                    border: "1px solid var(--color-accent, #d4af37)",
                    color: "var(--color-accent, #d4af37)",
                    padding: "6px 16px",
                    fontSize: "12px",
                    fontWeight: 500,
                    letterSpacing: "1px",
                    textTransform: "uppercase" as const,
                  }}
                >
                  {cred}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </section>
  );
}
