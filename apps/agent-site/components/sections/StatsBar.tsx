import type { StatItem } from "@/lib/types";

interface StatsBarProps {
  items: StatItem[];
}

export function StatsBar({ items }: StatsBarProps) {
  return (
    <section
      className="py-8 px-10 flex justify-center gap-12 flex-wrap"
      style={{ backgroundColor: "var(--color-primary)" }}
    >
      {items.map((item, i) => (
        <div key={i} className="text-center text-white">
          <div className="text-3xl font-extrabold" style={{ color: "var(--color-accent)" }}>
            {item.value}
          </div>
          <div className="text-xs uppercase tracking-widest mt-1">{item.label}</div>
        </div>
      ))}
    </section>
  );
}
