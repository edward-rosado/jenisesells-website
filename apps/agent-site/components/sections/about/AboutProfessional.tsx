import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/components/sections/types";

export function AboutProfessional({ agent, data }: AboutProps) {
  const displayName = getDisplayName(agent);
  const headshotUrl = getHeadshotUrl(agent);

  return (
    <section
      id="about"
      style={{
        background: "white",
        padding: "80px 40px",
      }}
    >
      <div
        style={{
          maxWidth: "1100px",
          margin: "0 auto",
          display: "flex",
          alignItems: "flex-start",
          gap: "56px",
          flexWrap: "wrap",
          justifyContent: "center",
        }}
      >
        {/* Rectangular headshot — professional, not circular */}
        {headshotUrl && (
          <div
            data-testid="headshot-wrapper"
            style={{
              width: "280px",
              height: "340px",
              borderRadius: "6px",
              overflow: "hidden",
              flexShrink: 0,
              position: "relative",
              border: "1px solid #e2e8f0",
            }}
          >
            <Image
              src={headshotUrl}
              alt={`Photo of ${displayName}`}
              fill
              style={{ objectFit: "cover", objectPosition: "top" }}
              sizes="280px"
            />
          </div>
        )}

        {/* Content */}
        <div style={{ maxWidth: "580px", flex: 1, minWidth: "280px" }}>
          <h2
            style={{
              fontSize: "30px",
              fontWeight: 700,
              color: "#0f172a",
              marginBottom: "20px",
              letterSpacing: "-0.3px",
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
                  color: "#475569",
                  lineHeight: 1.75,
                  marginBottom: "14px",
                }}
              >
                {paragraph}
              </p>
            ))
          ) : (
            <p
              style={{
                fontSize: "16px",
                color: "#475569",
                lineHeight: 1.75,
                marginBottom: "24px",
              }}
            >
              {data.bio}
            </p>
          )}

          {/* Designation / credential pills — blue accent border style */}
          {data.credentials && data.credentials.length > 0 && (
            <div
              style={{
                display: "flex",
                flexWrap: "wrap",
                gap: "10px",
                marginTop: "24px",
              }}
            >
              {data.credentials.map((cred) => (
                <span
                  key={cred}
                  style={{
                    background: "#eff6ff",
                    color: "#1d4ed8",
                    border: "1px solid #bfdbfe",
                    padding: "6px 16px",
                    borderRadius: "4px",
                    fontSize: "13px",
                    fontWeight: 600,
                    letterSpacing: "0.2px",
                  }}
                >
                  {cred}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
