import { Nav } from "@/components/Nav";
import {
  HeroCentered,
  StatsInline,
  ServicesIcons,
  StepsFriendly,
  SoldCards,
  TestimonialsBubble,
  ProfilesCards,
  CmaSection,
  AboutCard,
  Footer,
  ScrollRevealSection,
} from "@/components/sections";
import { type TemplateProps, getEnabledSections } from "./types";

export function WarmCommunity({ account, content, agent }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  const enabledSections = getEnabledSections(s);
  return (
    <>
      <Nav account={account} navigation={content.navigation} enabledSections={enabledSections} />
      <div style={{ paddingTop: "0" }}>
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
          />
        </ScrollRevealSection>
      )}
      {s.about?.enabled && (
        <ScrollRevealSection>
          <AboutCard agent={identity} data={s.about.data} />
        </ScrollRevealSection>
      )}
      <Footer agent={account} accountId={identity.id} />
      </div>
    </>
  );
}
