import Image from "next/image";
import type { HeroData } from "@/lib/types";

interface HeroProps {
  data: HeroData;
  agentPhotoUrl?: string;
  agentName?: string;
}

function safeHref(href: string): string {
  if (href.startsWith("#") || href.startsWith("/")) return href;
  try {
    const url = new URL(href);
    if (url.protocol === "https:" || url.protocol === "http:") return href;
  } catch { /* invalid URL */ }
  return "#";
}

function renderHeadline(headline: string, highlightWord?: string) {
  if (!highlightWord) return headline;
  const idx = headline.lastIndexOf(highlightWord);
  if (idx === -1) return headline;
  return (
    <>
      {headline.slice(0, idx)}
      <span style={{ color: "var(--color-accent)" }}>{highlightWord}</span>
      {headline.slice(idx + highlightWord.length)}
    </>
  );
}

export function Hero({ data, agentPhotoUrl, agentName }: HeroProps) {
  return (
    <section
      style={{
        background: "linear-gradient(135deg, var(--color-primary) 0%, var(--color-secondary) 60%, #43A047 100%)",
        color: "white",
        padding: "80px 40px",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "60px",
        flexWrap: "wrap",
        minHeight: "500px",
      }}
    >
      <div style={{ maxWidth: "520px" }}>
        <h1 style={{ fontSize: "44px", fontWeight: 800, lineHeight: 1.15, marginBottom: "10px" }}>
          {renderHeadline(data.headline, data.highlight_word)}
        </h1>
        <p style={{ fontSize: "20px", color: "#C8E6C9", marginBottom: "25px", fontStyle: "italic" }}>
          {data.tagline}
        </p>
        {data.body && (
          <p style={{ fontSize: "17px", color: "#E8F5E9", marginBottom: "30px" }}>
            {data.body}
          </p>
        )}
        <a
          href={safeHref(data.cta_link)}
          style={{
            display: "inline-block",
            background: "var(--color-accent)",
            color: "var(--color-primary)",
            padding: "16px 36px",
            borderRadius: "30px",
            fontSize: "17px",
            fontWeight: 700,
            transition: "all 0.3s",
          }}
        >
          {data.cta_text} &rarr;
        </a>
      </div>
      {agentPhotoUrl && (
        <div
          style={{
            width: "300px",
            height: "300px",
            borderRadius: "50%",
            overflow: "hidden",
            border: "5px solid var(--color-accent)",
            boxShadow: "0 15px 40px rgba(0,0,0,0.3)",
          }}
        >
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            width={300}
            height={300}
            style={{ width: "100%", height: "100%", objectFit: "cover" }}
            priority
          />
        </div>
      )}
    </section>
  );
}
