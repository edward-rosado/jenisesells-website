import { accounts, accountContent, agentContent } from "@/features/config/config-registry";

describe("Config validation", () => {
  const handles = Object.keys(accounts);

  describe.each(handles)("account: %s", (handle) => {
    const content = accountContent[handle];
    if (!content) return;

    const nav = content.navigation?.items ?? [];
    const sections = content.pages.home.sections;

    it("every enabled nav item maps to an enabled section", () => {
      const enabledNavHrefs = nav
        .filter((item) => item.enabled)
        .map((item) => item.href.replace("#", ""));

      for (const href of enabledNavHrefs) {
        const section = sections[href as keyof typeof sections];
        expect(section, `nav links to #${href} but section "${href}" is missing`).toBeDefined();
        if (section) {
          expect(section.enabled, `nav links to #${href} but section is disabled`).toBe(true);
        }
      }
    });
  });

  // Validate agent content: sections referenced by account nav must exist on agent page
  for (const handle of handles) {
    const entries = agentContent[handle];
    if (!entries) continue;

    for (const [agentId, content] of Object.entries(entries)) {
      describe(`agent: ${handle}/${agentId}`, () => {
        it("has valid section structure", () => {
          expect(content.pages.home.sections).toBeDefined();
        });

        it("enabled sections in agent content have valid data", () => {
          const sections = content.pages.home.sections;
          for (const [key, section] of Object.entries(sections)) {
            if (section && (section as { enabled: boolean }).enabled) {
              expect((section as { data: unknown }).data,
                `agent ${agentId} section "${key}" is enabled but has no data`
              ).toBeDefined();
            }
          }
        });
      });
    }
  }
});
