import type { AgentConfig, AgentContent } from "@/lib/types";

export const AGENT: AgentConfig = {
  id: "test-agent",
  identity: {
    name: "Jane Smith",
    title: "REALTOR",
    brokerage: "Best Homes Realty",
    phone: "555-123-4567",
    email: "jane@example.com",
    tagline: "Your Dream Home Awaits",
    languages: ["English", "Spanish"],
  },
  location: {
    state: "NJ",
    service_areas: ["Hoboken", "Jersey City"],
  },
  branding: {
    primary_color: "#1B5E20",
    secondary_color: "#2E7D32",
    accent_color: "#C8A951",
    font_family: "Segoe UI",
  },
  integrations: {
    form_handler: "formspree",
    form_handler_id: "abc123",
  },
};

export const AGENT_MINIMAL: AgentConfig = {
  id: "minimal-agent",
  identity: {
    name: "Bob Jones",
    phone: "555-000-1234",
    email: "bob@example.com",
  },
  location: {
    state: "TX",
  },
  branding: {},
};

export const CONTENT: AgentContent = {
  template: "emerald-classic",
  sections: {
    hero: {
      enabled: true,
      data: {
        headline: "Sell Your Home Fast",
        tagline: "Expert guidance every step",
        cta_text: "Get Free Report",
        cta_link: "#cma-form",
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
    services: {
      enabled: true,
      data: {
        items: [
          { title: "Market Analysis", description: "Deep market insights" },
          { title: "Photography", description: "Professional photos" },
          { title: "Negotiation", description: "Expert negotiation" },
        ],
      },
    },
    how_it_works: {
      enabled: true,
      data: {
        steps: [
          { number: 1, title: "Submit Info", description: "Fill out the form" },
          { number: 2, title: "Get Report", description: "Receive your CMA" },
          { number: 3, title: "Meet Agent", description: "Schedule walkthrough" },
        ],
      },
    },
    sold_homes: {
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
    cma_form: {
      enabled: true,
      data: {
        title: "What's Your Home Worth?",
        subtitle: "Get a free CMA today",
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
  },
};

export const CONTENT_ALL_DISABLED: AgentContent = {
  template: "emerald-classic",
  sections: {
    hero: { enabled: false, data: { headline: "", tagline: "", cta_text: "", cta_link: "" } },
    stats: { enabled: false, data: { items: [] } },
    services: { enabled: false, data: { items: [] } },
    how_it_works: { enabled: false, data: { steps: [] } },
    sold_homes: { enabled: false, data: { items: [] } },
    testimonials: { enabled: false, data: { items: [] } },
    cma_form: { enabled: false, data: { title: "", subtitle: "" } },
    about: { enabled: false, data: { bio: "", credentials: [] } },
    city_pages: { enabled: false, data: { cities: [] } },
  },
};
