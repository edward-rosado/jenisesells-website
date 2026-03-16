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
