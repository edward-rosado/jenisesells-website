import { Nav } from "@/features/shared/Nav";
import { HeroSplit } from "@/features/sections/heroes";
import { StatsCards } from "@/features/sections/stats";
import { ServicesClean } from "@/features/sections/services";
import { StepsTimeline } from "@/features/sections/steps";
import { SoldMinimal } from "@/features/sections/sold";
import { TestimonialsClean } from "@/features/sections/testimonials";
import { ProfilesClean } from "@/features/sections/profiles";
import { CmaSection, Footer, ScrollRevealSection } from "@/features/sections/shared";
import { AboutMinimal } from "@/features/sections/about";
import { type TemplateProps, type DefaultContent, getEnabledSections } from "./types";

export const defaultContent: DefaultContent = {
  hero: {
    title: "Modern Living Starts Here",
    subtitle: "Clean lines, clear results.",
    ctaText: "Get Your Free Home Value Report",
  },
  features: {
    title: "Services",
    subtitle: "Everything you need, nothing you don't.",
  },
  steps: {
    title: "How It Works",
    subtitle: "Simple and straightforward.",
  },
  gallery: {
    title: "Recently Sold",
    subtitle: "Proven results in your market.",
  },
  testimonials: {
    title: "What Clients Say",
  },
  profiles: {
    title: "Meet the Team",
    subtitle: "Dedicated professionals in your corner.",
  },
  contact: {
    title: "Get Started",
    subtitle: "Request your free home valuation today.",
  },
  about: {
    title: "About",
    subtitle: "Your trusted real estate partner.",
  },
};

export function ModernMinimal({ account, content, agent, locale }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  const enabledSections = getEnabledSections(s);
  return (
    <>
      <Nav account={account} navigation={content.navigation} enabledSections={enabledSections} locale={locale} />
      <div id="main-content" tabIndex={-1} style={{ paddingTop: "0" }}>
      {s.hero?.enabled && (
        <HeroSplit
          data={s.hero.data}
          agentPhotoUrl={identity.headshot_url ?? account.agent?.headshot_url}
          agentName={identity.name}
        />
      )}
      {s.stats?.enabled && s.stats.data.items.length > 0 && (
        <ScrollRevealSection>
          <StatsCards items={s.stats.data.items} sourceDisclaimer="Based on data from Zillow. Individual results may vary." />
        </ScrollRevealSection>
      )}
      {s.features?.enabled && (
        <ScrollRevealSection>
          <ServicesClean
            items={s.features.data.items}
            title={s.features.data.title}
            subtitle={s.features.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.steps?.enabled && (
        <ScrollRevealSection>
          <StepsTimeline
            steps={s.steps.data.steps}
            title={s.steps.data.title}
            subtitle={s.steps.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.gallery?.enabled && s.gallery.data.items.length > 0 && (
        <ScrollRevealSection>
          <SoldMinimal
            items={s.gallery.data.items}
            title={s.gallery.data.title}
            subtitle={s.gallery.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.testimonials?.enabled && s.testimonials.data.items.length > 0 && (
        <ScrollRevealSection>
          <TestimonialsClean
            items={s.testimonials.data.items}
            title={s.testimonials.data.title}
          />
        </ScrollRevealSection>
      )}
      {s.profiles?.enabled && s.profiles.data.items.length > 0 && (
        <ScrollRevealSection>
          <ProfilesClean
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
          <AboutMinimal agent={identity} data={s.about.data} />
        </ScrollRevealSection>
      )}
      <Footer agent={account} accountId={identity.id} locale={locale} />
      </div>
    </>
  );
}
