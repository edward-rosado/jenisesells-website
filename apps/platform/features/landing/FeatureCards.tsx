interface Feature {
  name: string;
  description: string;
  comingSoon: boolean;
}

const FEATURES: Feature[] = [
  {
    name: "Professional Website",
    description: "12 luxurious templates, white-label branded, deployed to your own domain in minutes. Mobile-responsive on Cloudflare's global edge.",
    comingSoon: false,
  },
  {
    name: "AI-Powered Setup",
    description: "Connect Google and AI builds your brand profile — colors, voice, personality, headshot — all in 10 minutes. No forms to fill out.",
    comingSoon: false,
  },
  {
    name: "CMA Automation",
    description: "Lead submits their address, AI generates a branded PDF with comparable sales data. Emailed automatically — no manual work.",
    comingSoon: false,
  },
  {
    name: "Lead Capture & Enrichment",
    description: "Website forms with bot protection. AI enriches every lead with property and market data. Instant notification to you.",
    comingSoon: false,
  },
  {
    name: "Multi-Language Support",
    description: "Serve bilingual communities. Your site auto-detects Spanish visitors. Emails and CMA reports drafted in the lead's language using your real voice.",
    comingSoon: false,
  },
  {
    name: "Legal Compliance",
    description: "State-specific disclosures, TCPA consent tracking, GDPR/CCPA, equal housing, and fair housing — built in, not bolted on.",
    comingSoon: false,
  },
  {
    name: "SEO & AEO",
    description: "Optimized for Google search AND AI answer engines like ChatGPT and Perplexity. Get found by humans and machines.",
    comingSoon: false,
  },
  {
    name: "WhatsApp Notifications",
    description: "Get instant lead alerts on WhatsApp. See lead details, property info, and score before you pick up the phone.",
    comingSoon: false,
  },
  {
    name: "Google Drive Integration",
    description: "All your leads, CMAs, and documents live in YOUR Google Drive. You own your data — always.",
    comingSoon: false,
  },
  {
    name: "Google Analytics",
    description: "Built-in visitor and lead tracking. Know who's visiting your site and what they're looking at.",
    comingSoon: false,
  },
  {
    name: "Auto-Replies",
    description: "AI responds to new leads instantly in your voice. Personalized, fast, and always on.",
    comingSoon: true,
  },
  {
    name: "Contract Drafting",
    description: "State-specific contracts auto-filled from lead data. Send for e-signature via DocuSign.",
    comingSoon: true,
  },
  {
    name: "MLS Automation",
    description: "Push listings to your MLS directly from your pipeline. No re-entering data.",
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
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
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
                  <span
                    className="text-xs px-2 py-0.5 rounded-full bg-gray-800 text-gray-400 whitespace-nowrap ml-2"
                    aria-label={`${feature.name} is coming soon`}
                  >
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
