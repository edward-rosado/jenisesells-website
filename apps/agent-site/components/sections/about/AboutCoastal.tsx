import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";

export function AboutCoastal({ agent, data }: AboutProps) {
  return (
    <section
      id="about"
      style={{
        padding: "70px 40px",
        background: "#fefcf8",
      }}
    >
      <div
        style={{
          maxWidth: "900px",
          margin: "0 auto",
          display: "flex",
          alignItems: "flex-start",
          gap: "48px",
          flexWrap: "wrap",
        }}
      >
        {agent.identity.headshot_url && (
          <div
            style={{
              width: "220px",
              height: "260px",
              borderRadius: "16px",
              overflow: "hidden",
              border: `4px solid var(--color-primary, #2c7a7b)`,
              flexShrink: 0,
              position: "relative",
            }}
          >
            <Image
              src={agent.identity.headshot_url}
              alt={`Photo of ${agent.identity.name}`}
              fill
              style={{ objectFit: "cover" }}
              sizes="220px"
            />
          </div>
        )}
        <div style={{ flex: "1 1 300px" }}>
          <h2
            style={{
              fontSize: "30px",
              fontWeight: 700,
              color: "var(--color-primary, #2c7a7b)",
              marginBottom: "20px",
            }}
          >
            {data.title || `About ${agent.identity.name}`}
          </h2>
          {Array.isArray(data.bio) ? (
            data.bio.map((paragraph, i) => (
              <p
                key={i}
                style={{
                  fontSize: "16px",
                  color: "#3a5a5a",
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
                fontSize: "16px",
                color: "#3a5a5a",
                lineHeight: 1.7,
                marginBottom: "24px",
              }}
            >
              {data.bio}
            </p>
          )}
          {data.credentials && data.credentials.length > 0 && (
            <div
              style={{
                display: "flex",
                flexWrap: "wrap",
                gap: "8px",
                marginTop: "20px",
              }}
            >
              {data.credentials.map((cred) => (
                <span
                  key={cred}
                  style={{
                    background: "var(--color-primary, #2c7a7b)",
                    color: "white",
                    padding: "6px 16px",
                    borderRadius: "20px",
                    fontSize: "13px",
                    fontWeight: 600,
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
