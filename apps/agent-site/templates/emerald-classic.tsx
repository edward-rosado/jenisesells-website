import type { AgentConfig, AgentContent } from "@/lib/types";
import { Nav } from "@/components/Nav";
import { Hero, StatsBar, Services, HowItWorks, SoldHomes, Testimonials, CmaForm, About, Footer } from "@/components/sections";

interface TemplateProps {
  agent: AgentConfig;
  content: AgentContent;
}

export function EmeraldClassic({ agent, content }: TemplateProps) {
  const s = content.sections;
  return (
    <>
      <Nav agent={agent} />
      <div className="pt-[74px]">
      {s.hero.enabled && <Hero agent={agent} data={s.hero.data} />}
      {s.stats.enabled && s.stats.data.items.length > 0 && <StatsBar items={s.stats.data.items} />}
      {s.services.enabled && <Services items={s.services.data.items} />}
      {s.how_it_works.enabled && <HowItWorks steps={s.how_it_works.data.steps} />}
      {s.sold_homes.enabled && s.sold_homes.data.items.length > 0 && <SoldHomes items={s.sold_homes.data.items} />}
      {s.testimonials.enabled && s.testimonials.data.items.length > 0 && <Testimonials items={s.testimonials.data.items} />}
      {s.cma_form.enabled && <CmaForm agent={agent} data={s.cma_form.data} />}
      {s.about.enabled && <About agent={agent} data={s.about.data} />}
      <Footer agent={agent} />
      </div>
    </>
  );
}
