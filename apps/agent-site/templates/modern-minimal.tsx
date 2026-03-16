import { Nav } from "@/components/Nav";
import {
  HeroSplit,
  StatsCards,
  ServicesClean,
  StepsTimeline,
  SoldMinimal,
  TestimonialsClean,
  CmaForm,
  AboutMinimal,
  Footer,
} from "@/components/sections";
import type { TemplateProps } from "./types";

export function ModernMinimal({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Nav agent={agent} />
      <div style={{ paddingTop: "0" }}>
      {s.hero.enabled && (
        <HeroSplit
          data={s.hero.data}
          agentPhotoUrl={agent.identity.headshot_url}
          agentName={agent.identity.name}
        />
      )}
      {s.stats.enabled && s.stats.data.items.length > 0 && (
        <StatsCards items={s.stats.data.items} sourceDisclaimer="Based on data from Zillow. Individual results may vary." />
      )}
      {s.services.enabled && (
        <ServicesClean
          items={s.services.data.items}
          title={s.services.data.title}
          subtitle={s.services.data.subtitle}
        />
      )}
      {s.how_it_works.enabled && (
        <StepsTimeline
          steps={s.how_it_works.data.steps}
          title={s.how_it_works.data.title}
          subtitle={s.how_it_works.data.subtitle}
        />
      )}
      {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && (
        <SoldMinimal
          items={s.sold_homes.data.items}
          title={s.sold_homes.data.title}
          subtitle={s.sold_homes.data.subtitle}
        />
      )}
      {s.testimonials.enabled && s.testimonials.data.items.length > 0 && (
        <TestimonialsClean
          items={s.testimonials.data.items}
          title={s.testimonials.data.title}
        />
      )}
      {s.cma_form.enabled && (
        <CmaForm
          agentId={agent.id}
          agentName={agent.identity.name}
          defaultState={agent.location.state}
          formHandler={agent.integrations?.form_handler}
          formHandlerId={agent.integrations?.form_handler_id}
          tracking={agent.integrations?.tracking}
          data={s.cma_form.data}
          serviceAreas={agent.location.service_areas}
        />
      )}
      {s.about.enabled && <AboutMinimal agent={agent} data={s.about.data} />}
      <Footer agent={agent} agentId={agent.id} />
      </div>
    </>
  );
}
