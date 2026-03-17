import Image from "next/image";
import type { AboutProps } from "@/components/sections/types";

export function AboutHomestead({ agent, data }: AboutProps) {
  return (
    <section
      id="about"
      style={{
        background: "var(--color-stone, #e8e2d8)",
        padding: "80px 40px",
        width: "100%",
        boxSizing: "border-box",
      }}
    >
      <div
        style={{
          maxWidth: "1100px",
          margin: "0 auto",
          display: "flex",
          flexDirection: "column",
          gap: "40px",
          alignItems: "center",
        }}
      >
        {/* Landscape-oriented agent photo */}
        {agent.identity.headshot_url && (
          <div
            style={{
              width: "640px",
              maxWidth: "100%",
              height: "300px",
              borderRadius: "8px",
              overflow: "hidden",
              position: "relative",
              border: "3px solid rgba(74,103,65,0.25)",
            }}
          >
            <Image
              src={agent.identity.headshot_url}
              alt={agent.identity.name}
              fill
              style={{ objectFit: "cover" }}
              sizes="(max-width: 768px) 100vw, 640px"
            />
          </div>
        )}

        <div style={{ maxWidth: "780px", width: "100%" }}>
          <h2
            style={{
              color: "var(--color-primary, #2d4a3e)",
              fontSize: "32px",
              fontWeight: 700,
              marginBottom: "20px",
              fontFamily: "Georgia, serif",
              textAlign: "center",
            }}
          >
            {data.title || `About ${agent.identity.name}`}
          </h2>

          <div style={{ textAlign: "center" }}>
            {Array.isArray(data.bio) ? (
              data.bio.map((paragraph, i) => (
                <p
                  key={i}
                  style={{
                    color: "#5a5040",
                    fontSize: "16px",
                    lineHeight: 1.75,
                    marginBottom: "14px",
                    fontFamily: "sans-serif",
                  }}
                >
                  {paragraph}
                </p>
              ))
            ) : (
              <p
                style={{
                  color: "#5a5040",
                  fontSize: "16px",
                  lineHeight: 1.75,
                  marginBottom: "14px",
                  fontFamily: "sans-serif",
                }}
              >
                {data.bio}
              </p>
            )}
          </div>

          {/* Green credential pills */}
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
                    background: "var(--color-accent, #4a6741)",
                    color: "white",
                    padding: "6px 16px",
                    borderRadius: "20px",
                    fontSize: "13px",
                    fontWeight: 600,
                    fontFamily: "sans-serif",
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
