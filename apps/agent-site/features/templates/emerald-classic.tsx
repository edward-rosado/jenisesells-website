import { Nav } from "@/features/shared/Nav";
import { HeroGradient } from "@/features/sections/heroes";
import { StatsBar } from "@/features/sections/stats";
import { ServicesGrid } from "@/features/sections/services";
import { StepsNumbered } from "@/features/sections/steps";
import { SoldGrid } from "@/features/sections/sold";
import { TestimonialsGrid } from "@/features/sections/testimonials";
import { ProfilesGrid } from "@/features/sections/profiles";
import { CmaSection, Footer, ScrollRevealSection } from "@/features/sections/shared";
import { AboutSplit } from "@/features/sections/about";
import { type TemplateProps, type DefaultContent, getEnabledSections } from "./types";

export const defaultContent: DefaultContent = {
  hero: {
    title: "Sell Your Home Fast",
    subtitle: "Expert guidance every step of the way",
    ctaText: "Get Your Free Home Value Report",
  },
  features: {
    title: "What I Do for You",
    subtitle: "Full-service representation from list to close.",
  },
  steps: {
    title: "How It Works",
    subtitle: "Three simple steps to get started.",
  },
  gallery: {
    title: "Recently Sold",
    subtitle: "Real results from real clients.",
  },
  testimonials: {
    title: "What My Clients Say",
  },
  profiles: {
    title: "Meet the Team",
    subtitle: "Experienced professionals ready to help.",
  },
  contact: {
    title: "What's Your Home Worth?",
    subtitle: "Get a free Comparative Market Analysis today.",
  },
  about: {
    title: "About",
    subtitle: "Get to know your agent.",
  },
};

export function EmeraldClassic({ account, content, agent, locale }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  const enabledSections = getEnabledSections(s);
  return (
    <>
      <Nav account={account} navigation={content.navigation} enabledSections={enabledSections} locale={locale} />
      <div id="main-content" tabIndex={-1} style={{ paddingTop: "0" }}>
      {s.hero?.enabled && (
        <HeroGradient
          data={s.hero.data}
          agentPhotoUrl={identity.headshot_url ?? account.agent?.headshot_url}
          agentName={identity.name}
        />
      )}
      {s.stats?.enabled && s.stats.data.items.length > 0 && (
        <ScrollRevealSection>
          <StatsBar items={s.stats.data.items} sourceDisclaimer="Based on data from Zillow. Individual results may vary." />
        </ScrollRevealSection>
      )}
      {s.features?.enabled && (
        <ScrollRevealSection>
          <ServicesGrid
            items={s.features.data.items}
            title={s.features.data.title}
            subtitle={s.features.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.steps?.enabled && (
        <ScrollRevealSection>
          <StepsNumbered
            steps={s.steps.data.steps}
            title={s.steps.data.title}
            subtitle={s.steps.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.gallery?.enabled && s.gallery.data.items.length > 0 && (
        <ScrollRevealSection>
          <SoldGrid
            items={s.gallery.data.items}
            title={s.gallery.data.title}
            subtitle={s.gallery.data.subtitle}
          />
        </ScrollRevealSection>
      )}
      {s.testimonials?.enabled && s.testimonials.data.items.length > 0 && (
        <ScrollRevealSection>
          <TestimonialsGrid
            items={s.testimonials.data.items}
            title={s.testimonials.data.title}
          />
        </ScrollRevealSection>
      )}
      {s.profiles?.enabled && s.profiles.data.items.length > 0 && (
        <ScrollRevealSection>
          <ProfilesGrid
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
          <AboutSplit agent={identity} data={s.about.data} />
        </ScrollRevealSection>
      )}
      <Footer agent={account} accountId={identity.id} locale={locale} />
      </div>
    </>
  );
}
