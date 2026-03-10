import type { AgentConfig, AboutData } from "@/lib/types";

interface AboutProps {
  agent: AgentConfig;
  data: AboutData;
}

export function About({ agent, data }: AboutProps) {
  return (
    <section className="py-16 px-10 max-w-4xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        About {agent.identity.name}
      </h2>
      <div className="text-gray-600 leading-relaxed text-center max-w-2xl mx-auto">
        <p>{data.bio}</p>
        {data.credentials && data.credentials.length > 0 && (
          <div className="flex justify-center gap-4 mt-6 flex-wrap">
            {data.credentials.map((cred) => (
              <span
                key={cred}
                className="px-4 py-2 rounded-full text-sm font-semibold"
                style={{ backgroundColor: "var(--color-primary)", color: "white" }}
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
