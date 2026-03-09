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

export function Hero({ data, agentPhotoUrl, agentName }: HeroProps) {
  return (
    <section
      className="min-h-[500px] flex items-center justify-center gap-16 flex-wrap px-10 py-20"
      style={{ background: "linear-gradient(135deg, var(--color-primary) 0%, var(--color-secondary) 60%)" }}
    >
      <div className="max-w-xl text-white">
        <h1 className="text-5xl font-extrabold leading-tight mb-3">
          {data.headline}
        </h1>
        <p className="text-xl italic opacity-80 mb-6">{data.tagline}</p>
        <a
          href={safeHref(data.cta_link)}
          className="inline-block px-9 py-4 rounded-full text-lg font-bold transition-transform hover:-translate-y-0.5"
          style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
        >
          {data.cta_text} &rarr;
        </a>
      </div>
      {agentPhotoUrl && (
        <div className="relative w-72 h-96 rounded-2xl overflow-hidden shadow-2xl">
          <Image
            src={agentPhotoUrl}
            alt={agentName ? `Photo of ${agentName}` : "Agent photo"}
            fill
            className="object-cover"
            sizes="(max-width: 768px) 100vw, 288px"
            priority
          />
        </div>
      )}
    </section>
  );
}
