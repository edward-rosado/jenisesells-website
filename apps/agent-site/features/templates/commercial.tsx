import { Nav } from "@/features/shared/Nav";
import { HeroCorporate } from "@/features/sections/heroes";
import { MarqueeBanner } from "@/features/sections/marquee";
import { StatsMetrics } from "@/features/sections/stats";
import { ServicesPremium } from "@/features/sections/services";
import { StepsCorporate } from "@/features/sections/steps";
import { SoldMetrics } from "@/features/sections/sold";
import { TestimonialsSpotlight } from "@/features/sections/testimonials";
import { ProfilesGrid } from "@/features/sections/profiles";
import { CmaSection, Footer, ScrollRevealSection } from "@/features/sections/shared";
import { AboutProfessional } from "@/features/sections/about";
import { type TemplateProps, getEnabledSections } from "./types";

export function Commercial({ account, content, agent }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  const enabledSections = getEnabledSections(s);
  return (
    <>
      <Nav account={account} navigation={content.navigation} enabledSections={enabledSections} />
      <div id="main-content" tabIndex={-1} style={{ paddingTop: "0" }}>
        {s.hero?.enabled && (
          <HeroCorporate
            data={s.hero.data}
            agentPhotoUrl={identity.headshot_url ?? account.agent?.headshot_url}
            agentName={identity.name}
          />
        )}
        {s.marquee?.enabled && s.marquee.data.items.length > 0 && (
          <MarqueeBanner
            items={s.marquee.data.items}
            title={s.marquee.data.title}
          />
        )}
        {s.stats?.enabled && s.stats.data.items.length > 0 && (
          <ScrollRevealSection>
            <StatsMetrics
              items={s.stats.data.items}
              sourceDisclaimer="Based on verified transaction records. Individual results may vary."
            />
          </ScrollRevealSection>
        )}
        {s.features?.enabled && (
          <ServicesPremium
            items={s.features.data.items}
            title={s.features.data.title}
            subtitle={s.features.data.subtitle}
          />
        )}
        {s.steps?.enabled && (
          <ScrollRevealSection>
            <StepsCorporate
              steps={s.steps.data.steps}
              title={s.steps.data.title}
              subtitle={s.steps.data.subtitle}
            />
          </ScrollRevealSection>
        )}
        {s.gallery?.enabled && s.gallery.data.items.length > 0 && (
          <ScrollRevealSection>
            <SoldMetrics
              items={s.gallery.data.items}
              title={s.gallery.data.title}
              subtitle={s.gallery.data.subtitle}
            />
          </ScrollRevealSection>
        )}
        {s.testimonials?.enabled && s.testimonials.data.items.length > 0 && (
          <ScrollRevealSection>
            <TestimonialsSpotlight
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
            />
          </ScrollRevealSection>
        )}
        {s.about?.enabled && (
          <ScrollRevealSection>
            <AboutProfessional agent={identity} data={s.about.data} />
          </ScrollRevealSection>
        )}
        <Footer agent={account} accountId={identity.id} />
      </div>
    </>
  );
}
