import type { AccountConfig, AgentConfig, ContentConfig } from "@/features/config/types";

export const ACCOUNT: AccountConfig = {
  handle: "test-agent",
  template: "emerald-classic",
  branding: {
    primary_color: "#1B5E20",
    secondary_color: "#2E7D32",
    accent_color: "#C8A951",
    font_family: "Segoe UI",
  },
  brokerage: {
    name: "Best Homes Realty",
    license_number: "123456",
    office_phone: "(732) 251-2500",
  },
  agent: {
    enabled: true,
    id: "test-agent",
    name: "Jane Smith",
    title: "REALTOR",
    phone: "555-123-4567",
    email: "jane@example.com",
    tagline: "Your Dream Home Awaits",
    languages: ["English", "Spanish"],
    headshot_url: undefined,
  },
  location: {
    state: "NJ",
    service_areas: ["Hoboken", "Jersey City"],
  },
  integrations: {},
  contact_info: [
    { type: "email", value: "jane@example.com", label: "Personal Email", is_preferred: false },
    { type: "phone", value: "555-123-4567", label: "Cell Phone", is_preferred: true },
    { type: "phone", value: "(732) 251-2500", ext: "714", label: "Office Phone", is_preferred: false },
  ],
};

export const ACCOUNT_MINIMAL: AccountConfig = {
  handle: "minimal-agent",
  template: "emerald-classic",
  branding: {},
  brokerage: { name: "Min Realty", license_number: "000" },
  agent: {
    enabled: true,
    id: "minimal-agent",
    name: "Bob Jones",
    title: "Agent",
    phone: "555-000-1234",
    email: "bob@example.com",
  },
  location: {
    state: "TX",
    service_areas: [],
  },
};

/** Account with broker but no agent — exercises broker fallback in identity resolution */
export const ACCOUNT_BROKER_ONLY: AccountConfig = {
  handle: "broker-only",
  template: "emerald-classic",
  branding: {},
  brokerage: { name: "Broker Realty", license_number: "999" },
  broker: { name: "Sam Broker", title: "Managing Broker" },
  location: { state: "NJ", service_areas: [] },
};

/** Account with neither agent nor broker — exercises brokerage.name fallback */
export const ACCOUNT_BROKERAGE_ONLY: AccountConfig = {
  handle: "brokerage-only",
  template: "emerald-classic",
  branding: {},
  brokerage: { name: "Brokerage LLC", license_number: "888" },
  location: { state: "NJ", service_areas: [] },
};

/** Explicit AgentConfig for testing agent prop passthrough */
export const AGENT_PROP: AgentConfig = {
  id: "explicit-agent",
  name: "Explicit Agent",
  title: "Senior REALTOR",
  phone: "555-999-0000",
  email: "explicit@example.com",
};

export const CONTENT: ContentConfig = {
  navigation: {
    items: [
      { label: "Why Choose Me", href: "#features", enabled: true },
      { label: "How It Works", href: "#steps", enabled: true },
      { label: "Recent Sales", href: "#gallery", enabled: true },
      { label: "Testimonials", href: "#testimonials", enabled: true },
      { label: "Team", href: "#profiles", enabled: true },
      { label: "Ready to Move?", href: "#contact_form", enabled: true },
      { label: "About", href: "#about", enabled: true },
    ],
  },
  pages: {
    home: {
      sections: {
        hero: {
          enabled: true,
          data: {
            headline: "Sell Your Home Fast",
            tagline: "Expert guidance every step",
            cta_text: "Get Free Report",
            cta_link: "#contact_form",
          },
        },
        stats: {
          enabled: true,
          data: {
            items: [
              { value: "150+", label: "Homes Sold" },
              { value: "$2.5M", label: "Total Volume" },
            ],
          },
        },
        features: {
          enabled: true,
          data: {
            items: [
              { title: "Market Analysis", description: "Deep market insights" },
              { title: "Photography", description: "Professional photos" },
              { title: "Negotiation", description: "Expert negotiation" },
            ],
          },
        },
        steps: {
          enabled: true,
          data: {
            steps: [
              { number: 1, title: "Submit Info", description: "Fill out the form" },
              { number: 2, title: "Get Report", description: "Receive your CMA" },
              { number: 3, title: "Meet Agent", description: "Schedule walkthrough" },
            ],
          },
        },
        gallery: {
          enabled: true,
          data: {
            items: [
              { address: "123 Main St", city: "Hoboken", state: "NJ", price: "$750,000" },
              { address: "456 Elm Ave", city: "Jersey City", state: "NJ", price: "$620,000" },
            ],
          },
        },
        testimonials: {
          enabled: true,
          data: {
            items: [
              { text: "Amazing service!", reviewer: "Alice B.", rating: 5, source: "Zillow" },
              { text: "Would recommend.", reviewer: "Tom C.", rating: 4 },
              { text: "Smooth process.", reviewer: "Sara D.", rating: 3 },
            ],
          },
        },
        profiles: {
          enabled: true,
          data: {
            title: "Our Team",
            subtitle: "Meet the experts",
            items: [
              { id: "agent-1", name: "Jane Smith", title: "REALTOR", phone: "555-123-4567", email: "jane@example.com" },
              { id: "agent-2", name: "John Doe", title: "Broker", phone: "555-987-6543", email: "john@example.com" },
            ],
          },
        },
        contact_form: {
          enabled: true,
          data: {
            title: "What's Your Home Worth?",
            subtitle: "Get a free CMA today",
            description: "Selling? Get a **free** home value report. Buying? Tell us what you need.",
          },
        },
        about: {
          enabled: true,
          data: {
            bio: "Jane Smith is a top agent in New Jersey.",
            credentials: ["ABR", "CRS"],
          },
        },
        city_pages: {
          enabled: false,
          data: { cities: [] },
        },
        marquee: {
          enabled: false,
          data: {
            title: "As Featured In",
            items: [
              { text: "LUXURY HOMES MAGAZINE" },
              { text: "WALL STREET JOURNAL", link: "https://example.com" },
              { text: "ARCHITECTURAL DIGEST" },
            ],
          },
        },
      },
    },
  },
};

/** Content with marquee enabled and items — for testing the marquee branch */
export const CONTENT_WITH_MARQUEE: ContentConfig = {
  ...CONTENT,
  pages: {
    home: {
      sections: {
        ...CONTENT.pages.home.sections,
        marquee: {
          enabled: true,
          data: {
            title: "As Featured In",
            items: [
              { text: "LUXURY HOMES MAGAZINE" },
              { text: "WALL STREET JOURNAL", link: "https://example.com" },
              { text: "ARCHITECTURAL DIGEST" },
            ],
          },
        },
      },
    },
  },
};

export const CONTENT_ALL_DISABLED: ContentConfig = {
  pages: {
    home: {
      sections: {
        hero: { enabled: false, data: { headline: "", tagline: "", cta_text: "", cta_link: "" } },
        stats: { enabled: false, data: { items: [] } },
        features: { enabled: false, data: { items: [] } },
        steps: { enabled: false, data: { steps: [] } },
        gallery: { enabled: false, data: { items: [] } },
        testimonials: { enabled: false, data: { items: [] } },
        profiles: { enabled: false, data: { items: [] } },
        contact_form: { enabled: false, data: { title: "", subtitle: "" } },
        about: { enabled: false, data: { bio: "", credentials: [] } },
        city_pages: { enabled: false, data: { cities: [] } },
        marquee: { enabled: false, data: { items: [] } },
      },
    },
  },
};
