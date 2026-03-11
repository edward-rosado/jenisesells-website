interface Feature {
  name: string;
  description: string;
  comingSoon: boolean;
}

const FEATURES: Feature[] = [
  {
    name: "Professional Website",
    description: "A custom-branded website deployed to your own domain in minutes.",
    comingSoon: false,
  },
  {
    name: "CMA Automation",
    description: "Generate comparative market analyses with AI-powered comp selection.",
    comingSoon: false,
  },
  {
    name: "Google Drive Integration",
    description: "Sync documents, photos, and contracts directly to your Google Drive.",
    comingSoon: false,
  },
  {
    name: "Lead Capture",
    description: "Collect and manage leads from your website with built-in forms.",
    comingSoon: false,
  },
  {
    name: "Auto-Replies",
    description: "Instantly respond to new leads with personalized messages.",
    comingSoon: true,
  },
  {
    name: "Contract Drafting & DocuSign",
    description: "Generate state-specific contracts and send for e-signature.",
    comingSoon: true,
  },
  {
    name: "Photographer Scheduling",
    description: "Book and manage listing photographers from your dashboard.",
    comingSoon: true,
  },
  {
    name: "MLS Listing Automation",
    description: "Push listings to your MLS directly from your pipeline.",
    comingSoon: true,
  },
];

export function FeatureCards() {
  return (
    <section className="py-20 px-6">
      <div className="max-w-6xl mx-auto">
        <h2 className="text-3xl md:text-4xl font-bold text-center mb-12">
          Everything You Need
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {FEATURES.map((feature) => (
            <div
              key={feature.name}
              data-testid="feature-card"
              className={`rounded-xl border p-6 transition-colors ${
                feature.comingSoon
                  ? "border-gray-800 bg-gray-900/50 opacity-75"
                  : "border-gray-700 bg-gray-900 hover:border-emerald-600"
              }`}
            >
              <div className="flex items-start justify-between mb-3">
                <h3 className="font-semibold text-white">{feature.name}</h3>
                {feature.comingSoon && (
                  <span className="text-xs px-2 py-0.5 rounded-full bg-gray-800 text-gray-400 whitespace-nowrap ml-2">
                    Coming Soon
                  </span>
                )}
              </div>
              <p
                data-testid="feature-description"
                className="text-sm text-gray-400 leading-relaxed"
              >
                {feature.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
