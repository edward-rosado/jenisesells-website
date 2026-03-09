import type { SoldHomeItem } from "@/lib/types";

interface SoldHomesProps {
  items: SoldHomeItem[];
}

export function SoldHomes({ items }: SoldHomesProps) {
  return (
    <section className="py-16 px-10 max-w-6xl mx-auto">
      <h2 className="text-3xl font-bold text-center mb-10" style={{ color: "var(--color-primary)" }}>
        Recently Sold
      </h2>
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-5">
        {items.map((item) => (
          <div key={`${item.address}-${item.city}`} className="bg-gray-50 rounded-lg p-5 text-center border border-gray-200">
            <span
              className="inline-block text-xs font-bold px-3 py-1 rounded-full mb-3"
              style={{ backgroundColor: "var(--color-accent)", color: "var(--color-primary)" }}
            >
              SOLD
            </span>
            <div className="text-xl font-extrabold" style={{ color: "var(--color-primary)" }}>
              {item.price}
            </div>
            <div className="text-xs text-gray-500 mt-1">
              {item.address}, {item.city}, {item.state}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
