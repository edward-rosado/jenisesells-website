import { Nav } from "@/features/shared/Nav";
import { HeroCentered } from "@/features/sections/heroes";
import { StatsInline } from "@/features/sections/stats";
import { ServicesIcons } from "@/features/sections/services";
import { StepsFriendly } from "@/features/sections/steps";
import { SoldCards } from "@/features/sections/sold";
import { TestimonialsBubble } from "@/features/sections/testimonials";
import { ProfilesCards } from "@/features/sections/profiles";
import { CmaSection, Footer, ScrollRevealSection } from "@/features/sections/shared";
import { AboutCard } from "@/features/sections/about";
import { type TemplateProps, type DefaultContent, getEnabledSections } from "./types";

export const defaultContent: DefaultContent = {
  hero: {
    title: "Your Home, Your Community",
    subtitle: "Helping families find their place.",
    ctaText: "Get Your Free Home Value Report",
  },
  features: {
    title: "How I Can Help",
    subtitle: "Personalized service for every family.",
  },
  steps: {
    title: "How It Works",
    subtitle: "Getting started is easy.",
  },
  gallery: {
    title: "Homes We've Sold",
    subtitle: "Neighbors who found their perfect home.",
  },
  testimonials: {
    title: "What Neighbors Say",
  },
  profiles: {
    title: "Our Team",
    subtitle: "Local experts who care.",
  },
  contact: {
    title: "Ready to Make a Move?",
    subtitle: "Let's find the right home for your family.",
  },
  about: {
    title: "About",
    subtitle: "Rooted in the community we serve.",
  },
};

export function WarmCommunity({ account, content, agent, locale }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  const enabledSections = getEnabledSections(s);
  return (
    <>
      <Nav account={account} navigation={content.navigation} enabledSections={enabledSections} locale={locale} />
      <div id="main-content" tabIndex={-1} style={{ paddingTop: "0" }}>
      {s.hero?.enabled && (
        <HeroCentered
          data={s.hero.data}
          agentPhotoUrl={identity.headshot_url ?? account.agent?.headshot_url}
          agentName={identity.name}
        />
      )}
      {s.stats?.enabled && s.stats.data.items.length > 0 && (
        <ScrollRevealSection>
          <StatsInline items={s.stats.data.items} sourceDisclaimer="Based on data from Zillow. Individual results may vary." />
        </ScrollRevealSection>
      )}
      {s.features?.enabled && (
        <ScrollRevealSection>
          <ServicesIcons
            items={s.features.data.items}
            title={s.features.data.title}
            subtitle={s.features.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.steps?.enabled && (
        <ScrollRevealSection>
          <StepsFriendly
            steps={s.steps.data.steps}
            title={s.steps.data.title}
            subtitle={s.steps.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.gallery?.enabled && s.gallery.data.items.length > 0 && (
        <ScrollRevealSection>
          <SoldCards
            items={s.gallery.data.items}
            title={s.gallery.data.title}
            subtitle={s.gallery.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.testimonials?.enabled && s.testimonials.data.items.length > 0 && (
        <ScrollRevealSection>
          <TestimonialsBubble
            items={s.testimonials.data.items}
            title={s.testimonials.data.title}
          />
        </ScrollRevealSection>
      )}
      {s.profiles?.enabled && s.profiles.data.items.length > 0 && (
        <ScrollRevealSection>
          <ProfilesCards
            items={s.profiles.data.items}
            title={s.profiles.data.title}
            subtitle={s.profiles.data.subtitle}
            accountId={account.handle}
          />
        </ScrollRevealSection>
      )}
      {s.contact_form?.enabled && (
        <ScrollRevealSection>
          <CmaSection
            accountId={identity.id}
            agentName={identity.name}
            defaultState={account.location.state}
            tracking={account.integrations?.tracking}
            data={s.contact_form.data}
            serviceAreas={account.location.service_areas}
            locale={locale}
          />
        </ScrollRevealSection>
      )}
      {s.about?.enabled && (
        <ScrollRevealSection>
          <AboutCard agent={identity} data={s.about.data} />
        </ScrollRevealSection>
      )}
      <Footer agent={account} accountId={identity.id} locale={locale} />
      </div>
    </>
  );
}
