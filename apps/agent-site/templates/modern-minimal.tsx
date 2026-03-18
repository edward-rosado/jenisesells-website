import { Nav } from "@/components/Nav";
import {
  HeroSplit,
  StatsCards,
  ServicesClean,
  StepsTimeline,
  SoldMinimal,
  TestimonialsClean,
  CmaSection,
  AboutMinimal,
  Footer,
} from "@/components/sections";
import type { TemplateProps } from "./types";

export function ModernMinimal({ account, content, agent }: TemplateProps) {
  const s = content.pages.home.sections;
  const identity = agent ?? account.agent ?? { id: account.handle, name: account.broker?.name ?? account.brokerage.name, title: account.broker?.title ?? "", phone: "", email: "" };
  return (
    <>
      <Nav account={account} navigation={content.navigation} />
      <div style={{ paddingTop: "0" }}>
      {s.hero?.enabled && (
        <HeroSplit
          data={s.hero.data}
          agentPhotoUrl={identity.headshot_url ?? account.agent?.headshot_url}
          agentName={identity.name}
        />
      )}
      {s.stats?.enabled && s.stats.data.items.length > 0 && (
        <StatsCards items={s.stats.data.items} sourceDisclaimer="Based on data from Zillow. Individual results may vary." />
      )}
      {s.features?.enabled && (
        <ServicesClean
          items={s.features.data.items}
          title={s.features.data.title}
          subtitle={s.features.data.subtitle}
        />
      )}
      {s.steps?.enabled && (
        <StepsTimeline
          steps={s.steps.data.steps}
          title={s.steps.data.title}
          subtitle={s.steps.data.subtitle}
        />
      )}
      {s.gallery?.enabled && s.gallery.data.items.length > 0 && (
        <SoldMinimal
          items={s.gallery.data.items}
          title={s.gallery.data.title}
          subtitle={s.gallery.data.subtitle}
        />
      )}
      {s.testimonials?.enabled && s.testimonials.data.items.length > 0 && (
        <TestimonialsClean
          items={s.testimonials.data.items}
          title={s.testimonials.data.title}
        />
      )}
      {s.contact_form?.enabled && (
        <CmaSection
          agentId={identity.id}
          agentName={identity.name}
          defaultState={account.location.state}
          tracking={account.integrations?.tracking}
          data={s.contact_form.data}
          serviceAreas={account.location.service_areas}
        />
      )}
      {s.about?.enabled && <AboutMinimal agent={account} data={s.about.data} />}
      <Footer agent={account} agentId={identity.id} />
      </div>
    </>
  );
}
