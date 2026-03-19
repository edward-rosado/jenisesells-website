import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/components/sections/types";

export function AboutGrace({ agent, data }: AboutProps) {
  const displayName = getDisplayName(agent);
  const headshotUrl = getHeadshotUrl(agent);

  return (
    <section
      id="about"
      style={{
        background: "#f8f6f3",
        padding: "80px 40px",
      }}
    >
      <div
        style={{
          maxWidth: "900px",
          margin: "0 auto",
          display: "flex",
          alignItems: "flex-start",
          gap: "56px",
          flexWrap: "wrap",
        }}
      >
        {/* Portrait photo */}
        {headshotUrl && (
          <div
            data-photo-wrapper
            style={{
              width: "260px",
              flexShrink: 0,
              position: "relative",
              aspectRatio: "3 / 4",
              overflow: "hidden",
              border: "1px solid var(--color-accent, #b8926a)",
            }}
          >
            <Image
              src={headshotUrl}
              alt={`Photo of ${displayName}`}
              fill
              style={{ objectFit: "cover" }}
              sizes="260px"
            />
          </div>
        )}

        {/* Bio content */}
        <div style={{ flex: "1", minWidth: "280px" }}>
          <h2
            style={{
              fontSize: "32px",
              fontWeight: 300,
              color: "var(--color-primary, #3d3028)",
              marginBottom: "28px",
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
                  color: "var(--color-secondary, #5a4a3a)",
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
                color: "var(--color-secondary, #5a4a3a)",
                lineHeight: 1.8,
                marginBottom: "16px",
                fontFamily: "var(--font-family, Georgia), serif",
                fontWeight: 300,
              }}
            >
              {data.bio}
            </p>
          )}

          {/* Credentials as comma-separated elegant text */}
          {data.credentials && data.credentials.length > 0 && (
            <p
              style={{
                fontSize: "13px",
                color: "var(--color-accent, #b8926a)",
                letterSpacing: "1px",
                marginTop: "20px",
                fontFamily: "var(--font-family, Georgia), serif",
                fontStyle: "italic",
              }}
            >
              {data.credentials.join(" · ")}
            </p>
          )}
        </div>
      </div>
    </section>
  );
}
