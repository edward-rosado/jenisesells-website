import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/components/sections/types";
import { safeMailtoHref, safeTelHref } from "@/lib/safe-contact";

export function AboutCard({ agent, data }: AboutProps) {
  const displayName = getDisplayName(agent);
  const headshotUrl = getHeadshotUrl(agent);
  const phone = "handle" in agent ? agent.agent?.phone : agent.phone;
  const email = "handle" in agent ? agent.agent?.email : agent.email;

  return (
    <section
      id="about"
      style={{
        padding: "70px 40px",
        background: "#FFF8F0",
      }}
    >
      <div style={{
        maxWidth: "700px",
        margin: "0 auto",
        background: "white",
        borderRadius: "24px",
        padding: "48px",
        boxShadow: "0 4px 20px rgba(0,0,0,0.06)",
        textAlign: "center",
      }}>
        {headshotUrl && (
          <div style={{
            width: "140px",
            height: "140px",
            borderRadius: "50%",
            overflow: "hidden",
            margin: "0 auto 24px",
            border: "4px solid var(--color-accent)",
          }}>
            <Image
              src={headshotUrl}
              alt={`Photo of ${displayName}`}
              width={140}
              height={140}
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
            />
          </div>
        )}
        <h2 style={{
          fontSize: "28px",
          fontWeight: 700,
          color: "#4A3728",
          marginBottom: "16px",
        }}>
          {data.title || `About ${displayName}`}
        </h2>
        {Array.isArray(data.bio) ? (
          data.bio.map((paragraph, i) => (
            <p key={i} style={{ fontSize: "16px", color: "#6B5A4A", lineHeight: 1.7, marginBottom: "12px" }}>
              {paragraph}
            </p>
          ))
        ) : (
          <p style={{
            fontSize: "16px",
            color: "#6B5A4A",
            lineHeight: 1.7,
            marginBottom: "24px",
          }}>
            {data.bio}
          </p>
        )}
        {data.credentials && data.credentials.length > 0 && (
          <ul
            aria-label="Credentials"
            style={{
              display: "flex",
              flexWrap: "wrap",
              justifyContent: "center",
              gap: "8px",
              marginBottom: "24px",
              listStyle: "none",
              padding: 0,
              margin: "0 0 24px",
            }}
          >
            {data.credentials.map((cred) => (
              <li
                key={cred}
                style={{
                  background: "#FFF0E0",
                  color: "#8B6914",
                  padding: "6px 16px",
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
        <div style={{
          fontSize: "14px",
          color: "#8B7355",
          lineHeight: 2,
        }}>
          {phone && (
            <div>
              <a
                href={safeTelHref(phone)}
                aria-label={`Call ${displayName}`}
                style={{ color: "inherit", textDecoration: "none" }}
              >
                {phone}
              </a>
            </div>
          )}
          {email && (
            <div>
              <a
                href={safeMailtoHref(email)}
                aria-label={`Email ${displayName}`}
                style={{ color: "inherit", textDecoration: "none" }}
              >
                {email}
              </a>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
