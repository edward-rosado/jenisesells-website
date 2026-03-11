interface TrustItem {
  label: string;
  detail: string;
}

const TRUST_ITEMS: TrustItem[] = [
  { label: "No Monthly Fees", detail: "One payment, lifetime access to all features." },
  { label: "Setup in Minutes", detail: "AI-powered onboarding gets you live in under 10 minutes." },
  { label: "7-Day Free Trial", detail: "Try everything before you pay. No credit card required." },
];

export function TrustStrip() {
  return (
    <section className="py-16 px-6 border-y border-gray-800/50">
      <div className="max-w-5xl mx-auto grid grid-cols-1 md:grid-cols-3 gap-8 text-center">
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
