// --- Agent Config (config/agents/{id}.json) ---

export interface AgentIdentity {
  name: string;
  title?: string;
  license_id?: string;
  brokerage?: string;
  brokerage_id?: string;
  phone: string;
  email: string;
  website?: string;
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
}

export interface AgentIntegrations {
  email_provider?: "gmail" | "outlook" | "smtp";
  hosting?: string;
  form_handler?: "formspree" | "custom";
  form_handler_id?: string;
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
  tagline: string;
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
  bio: string;
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
    services: SectionConfig<{ items: ServiceItem[] }>;
    how_it_works: SectionConfig<{ steps: StepItem[] }>;
    sold_homes: SectionConfig<{ items: SoldHomeItem[] }>;
    testimonials: SectionConfig<{ items: TestimonialItem[] }>;
    cma_form: SectionConfig<CmaFormData>;
    about: SectionConfig<AboutData>;
    city_pages: SectionConfig<{ cities: CityPageData[] }>;
  };
}
