import type { TestimonialItem } from "@/lib/types";

interface TestimonialsProps {
  items: TestimonialItem[];
}

export function Testimonials({ items }: TestimonialsProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        What My Clients Say
      </h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {items.map((item, i) => (
          <div key={i} className="bg-gray-50 rounded-xl p-7">
            <div className="text-lg mb-3" style={{ color: "var(--color-accent)" }}>
              {"★".repeat(item.rating)}{"☆".repeat(5 - item.rating)}
            </div>
            <p className="italic text-gray-600 text-sm leading-relaxed">{item.text}</p>
            <div className="mt-4 font-bold text-sm" style={{ color: "var(--color-primary)" }}>
              — {item.reviewer}
              {item.source && <span className="font-normal text-gray-400"> via {item.source}</span>}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
