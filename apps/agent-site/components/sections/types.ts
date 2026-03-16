import type {
  HeroData,
  StatItem,
  ServiceItem,
  StepItem,
  SoldHomeItem,
  TestimonialItem,
  AgentConfig,
  AboutData,
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

export interface ServicesProps {
  items: ServiceItem[];
  title?: string;
  subtitle?: string;
}

export interface StepsProps {
  steps: StepItem[];
  title?: string;
  subtitle?: string;
}

export interface SoldHomesProps {
  items: SoldHomeItem[];
  title?: string;
  subtitle?: string;
}

export interface TestimonialsProps {
  items: TestimonialItem[];
  title?: string;
}

export interface AboutProps {
  agent: AgentConfig;
  data: AboutData;
}

export function clampRating(rating: number): number {
  return Math.min(5, Math.max(0, Math.floor(rating || 0)));
}

export const FTC_DISCLAIMER =
  "Real reviews from real clients. Unedited excerpts from verified reviews on Zillow. No compensation was provided. Individual results may vary.";
