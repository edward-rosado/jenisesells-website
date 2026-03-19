import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/components/sections/types";

export function AboutSplit({ agent, data }: AboutProps) {
  const displayName = getDisplayName(agent);
  const headshotUrl = getHeadshotUrl(agent);

  return (
    <section
      id="about"
      style={{
        padding: "70px 40px",
        maxWidth: "1100px",
        margin: "0 auto",
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: "50px",
          flexWrap: "wrap",
          justifyContent: "center",
        }}
      >
        {headshotUrl && (
          <div
            style={{
              width: "250px",
              height: "250px",
              borderRadius: "50%",
              overflow: "hidden",
              border: "4px solid var(--color-accent)",
              flexShrink: 0,
              position: "relative",
            }}
          >
            <Image
              src={headshotUrl}
              alt={`Photo of ${displayName}`}
              fill
              style={{ objectFit: "cover" }}
              sizes="250px"
            />
          </div>
        )}
        <div style={{ maxWidth: "550px" }}>
          <h2
            style={{
              color: "var(--color-primary)",
              fontSize: "28px",
              marginBottom: "15px",
            }}
          >
            {data.title || `About ${displayName}`}
          </h2>
          {Array.isArray(data.bio) ? (
            data.bio.map((paragraph, i) => (
              <p key={i} style={{ color: "#555", fontSize: "15px", marginBottom: "12px" }}>
                {paragraph}
              </p>
            ))
          ) : (
            <p style={{ color: "#555", fontSize: "15px", marginBottom: "12px" }}>
              {data.bio}
            </p>
          )}
          {data.credentials && data.credentials.length > 0 && (
            <ul
              aria-label="Credentials"
              style={{
                display: "flex",
                flexWrap: "wrap",
                gap: "10px",
                marginTop: "15px",
                listStyle: "none",
                padding: 0,
              }}
            >
              {data.credentials.map((cred) => (
                <li
                  key={cred}
                  style={{
                    background: "#E8F5E9",
                    color: "var(--color-primary)",
                    padding: "6px 14px",
                    borderRadius: "20px",
                    fontSize: "13px",
                    fontWeight: 600,
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
