import type { AgentConfig, AgentContent } from "@/lib/types";
import { Nav } from "@/components/Nav";
import { Analytics } from "@/components/Analytics";
import { Hero, StatsBar, Services, HowItWorks, SoldHomes, Testimonials, CmaForm, About, Footer } from "@/components/sections";

interface TemplateProps {
  agent: AgentConfig;
  content: AgentContent;
}

export function EmeraldClassic({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Analytics tracking={agent.integrations?.tracking} />
      <Nav agent={agent} />
      <div style={{ paddingTop: "74px" }}>
      {s.hero.enabled && (
        <Hero
          data={s.hero.data}
          agentPhotoUrl={agent.identity.headshot_url}
          agentName={agent.identity.name}
        />
      )}
      {s.stats.enabled && s.stats.data.items.length > 0 && <StatsBar items={s.stats.data.items} />}
      {s.services.enabled && (
        <Services
          items={s.services.data.items}
          title={s.services.data.title}
          subtitle={s.services.data.subtitle}
        />
      )}
      {s.how_it_works.enabled && (
        <HowItWorks
          steps={s.how_it_works.data.steps}
          title={s.how_it_works.data.title}
          subtitle={s.how_it_works.data.subtitle}
        />
      )}
      {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && (
        <SoldHomes
          items={s.sold_homes.data.items}
          title={s.sold_homes.data.title}
          subtitle={s.sold_homes.data.subtitle}
        />
      )}
      {s.testimonials.enabled && s.testimonials.data.items.length > 0 && (
        <Testimonials
          items={s.testimonials.data.items}
          title={s.testimonials.data.title}
          subtitle={s.testimonials.data.subtitle}
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
        />
      )}
      {s.about.enabled && <About agent={agent} data={s.about.data} />}
      <Footer agent={agent} />
      </div>
    </>
  );
}
