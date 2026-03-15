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
  form_handler?: "formspree" | "custom";
  form_handler_id?: string;
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
}
