import { Nav } from "@/components/Nav";
import {
  HeroAiry,
  StatsElegant,
  ServicesRefined,
  StepsRefined,
  SoldElegant,
  TestimonialsQuote,
  CmaSection,
  AboutGrace,
  Footer,
} from "@/components/sections";
import type { TemplateProps } from "./types";

export function LightLuxury({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Nav agent={agent} navigation={content.navigation} contactInfo={content.contact_info} />
      <div style={{ paddingTop: "0" }}>
        {s.hero.enabled && (
          <HeroAiry
            data={s.hero.data}
            agentPhotoUrl={agent.identity.headshot_url}
            agentName={agent.identity.name}
          />
        )}
        {s.stats.enabled && s.stats.data.items.length > 0 && (
          <StatsElegant items={s.stats.data.items} sourceDisclaimer="Based on MLS data. Individual results may vary." />
        )}
        {s.services.enabled && (
          <ServicesRefined
            items={s.services.data.items}
            title={s.services.data.title}
            subtitle={s.services.data.subtitle}
          />
        )}
        {s.how_it_works.enabled && (
          <StepsRefined
            steps={s.how_it_works.data.steps}
            title={s.how_it_works.data.title}
            subtitle={s.how_it_works.data.subtitle}
          />
        )}
        {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && (
          <SoldElegant
            items={s.sold_homes.data.items}
            title={s.sold_homes.data.title}
            subtitle={s.sold_homes.data.subtitle}
          />
        )}
        {s.testimonials.enabled && s.testimonials.data.items.length > 0 && (
          <TestimonialsQuote
            items={s.testimonials.data.items}
            title={s.testimonials.data.title}
          />
        )}
        {s.cma_form.enabled && (
          <CmaSection
            agentId={agent.id}
            agentName={agent.identity.name}
            defaultState={agent.location.state}
            tracking={agent.integrations?.tracking}
            data={s.cma_form.data}
            serviceAreas={agent.location.service_areas}
          />
        )}
        {s.about.enabled && <AboutGrace agent={agent} data={s.about.data} />}
        <Footer agent={agent} agentId={agent.id} />
      </div>
    </>
  );
}
