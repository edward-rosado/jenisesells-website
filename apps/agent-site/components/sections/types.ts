import type {
  HeroData,
  StatItem,
  FeatureItem,
  StepItem,
  GalleryItem,
  TestimonialItem,
  AccountConfig,
  AgentConfig,
  AboutData,
  ProfileItem,
  MarqueeItem,
} from "@/lib/types";

export interface HeroProps {
  data: HeroData;
  agentPhotoUrl?: string;
  agentName?: string;
}

export interface StatsProps {
  items: StatItem[];
  sourceDisclaimer?: string;
}

export interface FeaturesProps {
  items: FeatureItem[];
  title?: string;
  subtitle?: string;
}

export interface StepsProps {
  steps: StepItem[];
  title?: string;
  subtitle?: string;
}

export interface GalleryProps {
  items: GalleryItem[];
  title?: string;
  subtitle?: string;
}

export interface TestimonialsProps {
  items: TestimonialItem[];
  title?: string;
}

export interface ProfilesProps {
  items: ProfileItem[];
  title?: string;
  subtitle?: string;
  accountId?: string;
}

export interface AboutProps {
  agent: AccountConfig | AgentConfig;
  data: AboutData;
}

export interface MarqueeProps {
  title?: string;
  items: MarqueeItem[];
}

/** @deprecated Use FeaturesProps */
export type ServicesProps = FeaturesProps;

/** @deprecated Use GalleryProps */
export type SoldHomesProps = GalleryProps;

/** Extract display name from either AccountConfig or AgentConfig */
export function getDisplayName(agent: AccountConfig | AgentConfig): string {
  if ("handle" in agent) {
    return agent.agent?.name ?? agent.broker?.name ?? agent.brokerage.name;
  }
  return agent.name;
}

/** Extract headshot URL from either AccountConfig or AgentConfig */
export function getHeadshotUrl(agent: AccountConfig | AgentConfig): string | undefined {
  if ("handle" in agent) {
    return agent.agent?.headshot_url;
  }
  return agent.headshot_url;
}

export function clampRating(rating: number): number {
  return Math.min(5, Math.max(0, Math.floor(rating || 0)));
}

export const FTC_DISCLAIMER =
  "Real reviews from real clients. Unedited excerpts from verified reviews on Zillow. No compensation was provided. Individual results may vary.";

