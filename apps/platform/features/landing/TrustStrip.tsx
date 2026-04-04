interface TrustItem {
  label: string;
  detail: string;
}

const TRUST_ITEMS: TrustItem[] = [
  { label: "14 Days Free", detail: "Try everything with no commitment." },
  { label: "$14.99/mo After", detail: "All features included. No add-ons, no surprises." },
  { label: "Live in 10 Minutes", detail: "AI-powered setup. Connect Google and you're done." },
  { label: "English + Spanish", detail: "Serve bilingual communities with auto-detected language." },
];

export function TrustStrip() {
  return (
    <section className="py-16 px-6 border-y border-gray-800/50">
      <div className="max-w-5xl mx-auto grid grid-cols-1 md:grid-cols-4 gap-8 text-center">
        {TRUST_ITEMS.map((item) => (
          <div key={item.label} data-testid="trust-item" className="space-y-2">
            <h3 className="text-lg font-semibold text-emerald-400">{item.label}</h3>
            <p className="text-sm text-gray-400">{item.detail}</p>
          </div>
        ))}
      </div>
    </section>
  );
}
