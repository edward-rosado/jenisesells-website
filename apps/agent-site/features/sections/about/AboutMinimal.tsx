import Image from "next/image";
import type { AboutProps } from "@/features/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/features/sections/types";

export function AboutMinimal({ agent, data }: AboutProps) {
  const displayName = getDisplayName(agent);
  const headshotUrl = getHeadshotUrl(agent);

  return (
    <section
      id="about"
      style={{
        padding: "80px 40px",
        maxWidth: "900px",
        margin: "0 auto",
      }}
    >
      <div style={{
        display: "flex",
        alignItems: "flex-start",
        gap: "40px",
        flexWrap: "wrap",
        justifyContent: "center",
      }}>
        {headshotUrl && (
          <div style={{
            width: "220px",
            height: "280px",
            borderRadius: "12px",
            overflow: "hidden",
            flexShrink: 0,
            position: "relative",
          }}>
            <Image
              src={headshotUrl}
              alt={`Photo of ${displayName}`}
              fill
              style={{ objectFit: "cover" }}
              sizes="220px"
            />
          </div>
        )}
        <div style={{ maxWidth: "500px", flex: 1 }}>
          <h2 style={{
            color: "#1a1a1a",
            fontSize: "28px",
            fontWeight: 600,
            marginBottom: "16px",
            letterSpacing: "-0.3px",
          }}>
            {data.title || `About ${displayName}`}
          </h2>
          {Array.isArray(data.bio) ? (
            data.bio.map((paragraph, i) => (
              <p key={i} style={{ color: "#666", fontSize: "15px", marginBottom: "12px", lineHeight: 1.7 }}>
                {paragraph}
              </p>
            ))
          ) : (
            <p style={{ color: "#666", fontSize: "15px", marginBottom: "12px", lineHeight: 1.7 }}>
              {data.bio}
            </p>
          )}
          {data.credentials && data.credentials.length > 0 && (
            <p style={{
              marginTop: "16px",
              fontSize: "13px",
              color: "#767676",
            }}>
              {data.credentials.join(" · ")}
            </p>
          )}
        </div>
      </div>
    </section>
  );
}
