import type { AgentConfig, HeroData } from "@/lib/types";

interface HeroProps {
  agent: AgentConfig;
  data: HeroData;
}

export function Hero({ agent, data }: HeroProps) {
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
          href={data.cta_link}
          className="inline-block px-9 py-4 rounded-full text-lg font-bold transition-transform hover:-translate-y-0.5"
          style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
        >
          {data.cta_text} &rarr;
        </a>
      </div>
    </section>
  );
}
