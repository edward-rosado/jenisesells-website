import { Nav } from "@/features/shared/Nav";
import { HeroBold } from "@/features/sections/heroes";
import { StatsCompact } from "@/features/sections/stats";
import { ServicesPills } from "@/features/sections/services";
import { StepsCards } from "@/features/sections/steps";
import { SoldCarousel } from "@/features/sections/sold";
import { TestimonialsStack } from "@/features/sections/testimonials";
import { ProfilesGrid } from "@/features/sections/profiles";
import { CmaSection, Footer, ScrollRevealSection } from "@/features/sections/shared";
import { AboutCompact } from "@/features/sections/about";
import { type TemplateProps, type DefaultContent, getEnabledSections } from "./types";

export const defaultContent: DefaultContent = {
  hero: {
    title: "Live in the Heart of the City",
    subtitle: "Urban living, elevated.",
    ctaText: "Find Your Urban Home",
  },
  features: {
    title: "Services",
    subtitle: "Expert guidance for city buyers and sellers.",
  },
  steps: {
    title: "How It Works",
    subtitle: "From search to keys in hand.",
  },
  gallery: {
    title: "Recent Sales",
    subtitle: "Urban properties sold at top dollar.",
  },
  testimonials: {
    title: "Client Reviews",
  },
  profiles: {
    title: "Meet the Team",
    subtitle: "Urban market specialists.",
  },
  contact: {
    title: "Let's Connect",
    subtitle: "Start your urban real estate journey today.",
  },
  about: {
    title: "About",
    subtitle: "Your urban real estate expert.",
  },
};

export function UrbanLoft({ account, content, agent, locale }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  const enabledSections = getEnabledSections(s);
  return (
    <>
      <Nav account={account} navigation={content.navigation} enabledSections={enabledSections} locale={locale} />
      <div id="main-content" tabIndex={-1} style={{ paddingTop: "0" }}>
        {s.hero?.enabled && (
          <HeroBold
            data={s.hero.data}
            agentPhotoUrl={identity.headshot_url ?? account.agent?.headshot_url}
            agentName={identity.name}
          />
        )}
        {s.stats?.enabled && s.stats.data.items.length > 0 && (
          <ScrollRevealSection>
            <StatsCompact
              items={s.stats.data.items}
              sourceDisclaimer="Based on MLS data and agent records. Individual results may vary."
            />
          </ScrollRevealSection>
        )}
        {s.features?.enabled && (
          <ScrollRevealSection>
            <ServicesPills
              items={s.features.data.items}
              title={s.features.data.title}
              subtitle={s.features.data.subtitle}
            />
          </ScrollRevealSection>
        )}
        {s.steps?.enabled && (
          <ScrollRevealSection>
            <StepsCards
              steps={s.steps.data.steps}
              title={s.steps.data.title}
              subtitle={s.steps.data.subtitle}
            />
          </ScrollRevealSection>
        )}
        {s.gallery?.enabled && s.gallery.data.items.length > 0 && (
          <ScrollRevealSection>
            <SoldCarousel
              items={s.gallery.data.items}
              title={s.gallery.data.title}
              subtitle={s.gallery.data.subtitle}
            />
          </ScrollRevealSection>
        )}
        {s.testimonials?.enabled && s.testimonials.data.items.length > 0 && (
          <ScrollRevealSection>
            <TestimonialsStack
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
            <AboutCompact agent={identity} data={s.about.data} />
          </ScrollRevealSection>
        )}
        <Footer agent={account} accountId={identity.id} locale={locale} />
      </div>
    </>
  );
}
