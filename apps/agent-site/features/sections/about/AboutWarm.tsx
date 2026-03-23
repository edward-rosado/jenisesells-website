import Image from "next/image";
import type { AboutProps } from "@/features/sections/types";
import { getDisplayName, getHeadshotUrl } from "@/features/sections/types";

export function AboutWarm({ agent, data }: AboutProps) {
  const displayName = getDisplayName(agent);
  const headshotUrl = getHeadshotUrl(agent);

  return (
    <section
      id="about"
      style={{
        background: "#f0f7f4",
        padding: "70px 40px",
      }}
    >
      <div
        style={{
          maxWidth: "700px",
          margin: "0 auto",
          textAlign: "center",
        }}
      >
        {/* Large circular photo with accent border */}
        {headshotUrl && (
          <div
            style={{
              width: "200px",
              height: "200px",
              borderRadius: "50%",
              overflow: "hidden",
              margin: "0 auto 28px",
              border: "4px solid var(--color-accent, #5a9e7c)",
              boxShadow: "0 6px 20px rgba(90,158,124,0.2)",
            }}
          >
            <Image
              src={headshotUrl}
              alt={`Photo of ${displayName}`}
              width={200}
              height={200}
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
            />
          </div>
        )}

        <h2
          style={{
            fontSize: "32px",
            fontWeight: 600,
            color: "var(--color-primary, #2d4a3e)",
            marginBottom: "24px",
            fontFamily: "var(--font-family, Nunito), sans-serif",
          }}
        >
          {data.title ?? `About ${displayName}`}
        </h2>

        {/* First-person bio paragraphs */}
        {Array.isArray(data.bio) ? (
          data.bio.map((paragraph, i) => (
            <p
              key={i}
              style={{
                fontSize: "16px",
                color: "#4a6b5a",
                lineHeight: 1.7,
                marginBottom: "14px",
                textAlign: "left",
              }}
            >
              {paragraph}
            </p>
          ))
        ) : (
          <p
            style={{
              fontSize: "16px",
              color: "#4a6b5a",
              lineHeight: 1.7,
              marginBottom: "28px",
              textAlign: "left",
            }}
          >
            {data.bio}
          </p>
        )}

        {/* Credentials as green pills */}
        {data.credentials && data.credentials.length > 0 && (
          <div
            style={{
              display: "flex",
              flexWrap: "wrap",
              justifyContent: "center",
              gap: "10px",
              marginTop: "24px",
            }}
          >
            {data.credentials.map((cred) => (
              <span
                key={cred}
                style={{
                  background: "var(--color-accent, #5a9e7c)",
                  color: "white",
                  padding: "6px 18px",
                  borderRadius: "20px",
                  fontSize: "13px",
                  fontWeight: 600,
                  fontFamily: "var(--font-family, Nunito), sans-serif",
                }}
              >
                {cred}
              </span>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
