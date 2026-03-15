import Image from "next/image";
import type { AgentConfig, AboutData } from "@/lib/types";

interface AboutProps {
  agent: AgentConfig;
  data: AboutData;
}

export function About({ agent, data }: AboutProps) {
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
        {agent.identity.headshot_url && (
          <div
            style={{
              width: "250px",
              height: "250px",
              borderRadius: "50%",
              overflow: "hidden",
              border: "4px solid #C8A951",
              flexShrink: 0,
              position: "relative",
            }}
          >
            <Image
              src={agent.identity.headshot_url}
              alt={agent.identity.name}
              fill
              style={{ objectFit: "cover" }}
              sizes="250px"
            />
          </div>
        )}
        <div style={{ maxWidth: "550px" }}>
          <h2
            style={{
              color: "#1B5E20",
              fontSize: "28px",
              marginBottom: "15px",
            }}
          >
            {data.title || `About ${agent.identity.name}`}
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
                    color: "#1B5E20",
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
