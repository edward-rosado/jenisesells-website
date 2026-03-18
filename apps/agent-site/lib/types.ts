// --- Account Config (config/accounts/{handle}/account.json) ---

export interface AccountConfig {
  handle: string;
  template: string;
  branding: AccountBranding;
  brokerage: BrokerageInfo;
  broker?: BrokerInfo;
  agent?: AccountAgent;
  location: AccountLocation;
  integrations?: AccountIntegrations;
  contact_info?: ContactMethod[];
  compliance?: ComplianceInfo;
}

export interface AccountAgent {
  enabled: boolean;
  id: string;
  name: string;
  title: string;
  phone: string;
  email: string;
  headshot_url?: string;
  license_number?: string;
  languages?: string[];
  tagline?: string;
  credentials?: string[];
}

export interface AgentConfig {
  id: string;
  name: string;
  title: string;
  phone: string;
  email: string;
  headshot_url?: string;
  license_number?: string;
  languages?: string[];
  tagline?: string;
  credentials?: string[];
}

export interface AccountBranding {
  primary_color?: string;
  secondary_color?: string;
  accent_color?: string;
  font_family?: string;
  logo_url?: string;
}

export interface BrokerageInfo {
  name: string;
  license_number: string;
  office_address?: string;
  office_phone?: string;
}

export interface BrokerInfo {
  name: string;
  title?: string;
  headshot_url?: string;
  bio?: string;
}

export interface AccountLocation {
  state: string;
  service_areas: string[];
}

export interface AccountIntegrations {
  email_provider?: "gmail" | "outlook" | "smtp";
  hosting?: string;
  tracking?: AccountTracking;
}

export interface AccountTracking {
  google_analytics_id?: string;
  google_ads_id?: string;
  google_ads_conversion_label?: string;
  meta_pixel_id?: string;
  gtm_container_id?: string;
}

export interface ContactMethod {
  type: "email" | "phone";
  value: string;
  ext?: string | null;
  label: string;
  is_preferred: boolean;
}

export interface ComplianceInfo {
  state_form?: string;
  licensing_body?: string;
  disclosure_requirements?: string[];
}

// --- Content Config (content.json — account or agent level) ---

export interface ContentConfig {
  navigation?: NavigationConfig;
  pages: {
    home: { sections: PageSections };
    thank_you?: ThankYouData;
  };
}

export interface NavigationConfig {
  items: NavItem[];
}

export interface NavItem {
  label: string;
  href: string;
  enabled: boolean;
}

// --- Page Sections ---

export interface SectionConfig<T = Record<string, unknown>> {
  enabled: boolean;
  data: T;
}

export interface PageSections {
  hero?: SectionConfig<HeroData>;
  stats?: SectionConfig<StatsData>;
  features?: SectionConfig<FeaturesData>;
  steps?: SectionConfig<StepsData>;
  gallery?: SectionConfig<GalleryData>;
  testimonials?: SectionConfig<TestimonialsData>;
  profiles?: SectionConfig<ProfilesData>;
  contact_form?: SectionConfig<ContactFormData>;
  about?: SectionConfig<AboutData>;
  city_pages?: SectionConfig<CityPagesData>;
}

// --- Section Data Types ---

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
export type StatsData = { items: StatItem[] };

export interface FeatureItem {
  title: string;
  description: string;
  icon?: string;
}
export type FeaturesData = { title?: string; subtitle?: string; items: FeatureItem[] };

export interface StepItem {
  number: number;
  title: string;
  description: string;
}
export type StepsData = { title?: string; subtitle?: string; steps: StepItem[] };

export interface GalleryItem {
  address: string;
  city: string;
  state: string;
  price: string;
  sold_date?: string;
  image_url?: string;
}
export type GalleryData = { title?: string; subtitle?: string; items: GalleryItem[] };

export interface TestimonialItem {
  text: string;
  reviewer: string;
  rating: number;
  source?: string;
}
export type TestimonialsData = { title?: string; subtitle?: string; items: TestimonialItem[] };

// New: Profiles section
export interface ProfileItem {
  id: string;
  name: string;
  title: string;
  headshot_url?: string;
  phone?: string;
  email?: string;
  link?: string;
}
export interface ProfilesData {
  title?: string;
  subtitle?: string;
  items: ProfileItem[];
}

export interface ContactFormData {
  title: string;
  subtitle: string;
  description?: string;
}

export interface AboutData {
  title?: string;
  bio: string | string[];
  credentials?: string[];
  image_url?: string;
}

export interface ThankYouData {
  heading: string;
  subheading: string;
  body?: string;
  disclaimer?: string;
  cta_call?: string;
  cta_back?: string;
}

export interface CityPageData {
  slug: string;
  city: string;
  state: string;
  county: string;
  highlights: string[];
  market_snapshot: string;
}
export type CityPagesData = { cities: CityPageData[] };
