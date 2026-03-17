// --- Agent Config (config/agents/{id}.json) ---

export interface AgentIdentity {
  name: string;
  title?: string;
  license_id?: string;
  brokerage?: string;
  brokerage_id?: string;
  phone: string;
  office_phone?: string;
  email: string;
  website?: string;
  headshot_url?: string;
  languages?: string[];
  tagline?: string;
}

export interface AgentLocation {
  state: string;
  office_address?: string;
  service_areas?: string[];
}

export interface AgentBranding {
  primary_color?: string;
  secondary_color?: string;
  accent_color?: string;
  font_family?: string;
  logo_url?: string;
}

export interface AgentTracking {
  google_analytics_id?: string;    // GA4 Measurement ID (G-XXXXXXXX)
  google_ads_id?: string;          // Google Ads conversion ID (AW-XXXXXXXX)
  google_ads_conversion_label?: string; // Conversion label for CMA form
  meta_pixel_id?: string;          // Meta/Facebook Pixel ID
  gtm_container_id?: string;       // Google Tag Manager container (GTM-XXXXXXXX)
}

export interface AgentIntegrations {
  email_provider?: "gmail" | "outlook" | "smtp";
  hosting?: string;
  tracking?: AgentTracking;
}

export interface AgentCompliance {
  state_form?: string;
  licensing_body?: string;
  disclosure_requirements?: string[];
}

export interface AgentConfig {
  id: string;
  identity: AgentIdentity;
  location: AgentLocation;
  branding: AgentBranding;
  integrations?: AgentIntegrations;
  compliance?: AgentCompliance;
}

// --- Agent Content (config/agents/{id}.content.json) ---

export interface SectionConfig<T = Record<string, unknown>> {
  enabled: boolean;
  data: T;
}

export interface HeroData {
  headline: string;
  highlight_word?: string;
  tagline: string;
  body?: string;
  cta_text: string;
  cta_link: string;
}

export interface StatItem {
  value: string;
  label: string;
}

export interface ServiceItem {
  title: string;
  description: string;
  icon?: string;
  category?: string;  // For two-tier service grouping (e.g., commercial)
}

export interface StepItem {
  number: number;
  title: string;
  description: string;
}

export interface SoldHomeItem {
  address: string;
  city: string;
  state: string;
  price: string;
  sold_date?: string;
  image_url?: string;
  // Optional fields for specialized templates
  property_type?: string;    // "Office", "Oceanfront", "Estate"
  sq_ft?: string;            // "45,000 SF"
  cap_rate?: string;         // "6.2%"
  noi?: string;              // "$280,000"
  badge_label?: string;      // Override "SOLD" text (e.g., "CLOSED")
  features?: Array<{ label: string; value: string }>;  // e.g., [{ label: "Lot", value: "5 acres" }]
  client_quote?: string;     // For success story display
  client_name?: string;      // For success story attribution
  tags?: string[];           // Multiple property tags (e.g., ["Oceanfront", "Beach Access"])
}

export interface TestimonialItem {
  text: string;
  reviewer: string;
  rating: number;
  source?: string;
}

export interface CmaFormData {
  title: string;
  subtitle: string;
  description?: string;
}

export interface ThankYouData {
  heading: string;
  subheading: string;
  body?: string;
  disclaimer?: string;
  cta_call?: string;
  cta_back?: string;
}

export interface NavItem {
  label: string;
  section: string;
}

export interface ContactMethod {
  type: "email" | "phone";
  value: string;
  ext?: string | null;
  label: string;
  is_preferred: boolean;
}

export interface AboutData {
  title?: string;
  bio: string | string[];
  credentials?: string[];
}

export interface CityPageData {
  slug: string;
  city: string;
  state: string;
  county: string;
  highlights: string[];
  market_snapshot: string;
}

export interface AgentContent {
  template: string;
  navigation?: {
    items: NavItem[];
  };
  contact_info?: ContactMethod[];
  sections: {
    hero: SectionConfig<HeroData>;
    stats: SectionConfig<{ items: StatItem[] }>;
    services: SectionConfig<{ title?: string; subtitle?: string; items: ServiceItem[] }>;
    how_it_works: SectionConfig<{ title?: string; subtitle?: string; steps: StepItem[] }>;
    sold_homes: SectionConfig<{ title?: string; subtitle?: string; items: SoldHomeItem[] }>;
    testimonials: SectionConfig<{ title?: string; subtitle?: string; items: TestimonialItem[] }>;
    cma_form: SectionConfig<CmaFormData>;
    about: SectionConfig<AboutData>;
    city_pages: SectionConfig<{ cities: CityPageData[] }>;
  };
  pages?: {
    thank_you?: ThankYouData;
  };
}
