import type { ServiceItem } from "@/lib/types";

interface ServicesProps {
  items: ServiceItem[];
}

export function Services({ items }: ServicesProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        What I Do for You
      </h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {items.map((item, i) => (
          <div
            key={i}
            className="bg-gray-50 rounded-xl p-7 border-l-4 transition-transform hover:-translate-y-1 hover:shadow-lg"
            style={{ borderLeftColor: "var(--color-secondary)" }}
          >
            <h3 className="text-lg font-bold mb-2" style={{ color: "var(--color-primary)" }}>
              {item.title}
            </h3>
            <p className="text-gray-600 text-sm">{item.description}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
