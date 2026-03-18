import type {
  FeatureItem,
  GalleryItem,
  ContactFormData,
  AccountConfig,
  AccountAgent,
  AgentConfig,
  ContentConfig,
  PageSections,
  ProfileItem,
  NavItem,
} from "@/lib/types";

describe("types.ts — type smoke tests", () => {
  it("FeatureItem replaces ServiceItem", () => {
    const item: FeatureItem = { title: "A", description: "B" };
    expect(item.title).toBe("A");
  });

  it("GalleryItem replaces SoldHomeItem", () => {
    const item: GalleryItem = { address: "1 Main", city: "NY", state: "NY", price: "$1" };
    expect(item.address).toBe("1 Main");
  });

  it("ContactFormData replaces CmaFormData", () => {
    const data: ContactFormData = { title: "T", subtitle: "S" };
    expect(data.title).toBe("T");
  });

  it("AccountConfig has required fields", () => {
    const cfg: AccountConfig = {
      handle: "test",
      template: "emerald-classic",
      branding: {},
      brokerage: { name: "B", license_number: "123" },
      location: { state: "NJ", service_areas: [] },
    };
    expect(cfg.handle).toBe("test");
  });

  it("AccountAgent has enabled flag", () => {
    const agent: AccountAgent = {
      enabled: true,
      id: "a",
      name: "A",
      title: "T",
      phone: "1",
      email: "a@b.com",
    };
    expect(agent.enabled).toBe(true);
  });

  it("AgentConfig is the small agent-identity type", () => {
    const agent: AgentConfig = {
      id: "a",
      name: "A",
      title: "T",
      phone: "1",
      email: "a@b.com",
    };
    expect(agent.id).toBe("a");
  });

  it("NavItem has href and enabled", () => {
    const nav: NavItem = { label: "Home", href: "#hero", enabled: true };
    expect(nav.href).toBe("#hero");
    expect(nav.enabled).toBe(true);
  });

  it("ProfileItem has id and optional link", () => {
    const p: ProfileItem = { id: "x", name: "X", title: "Agent" };
    expect(p.link).toBeUndefined();
  });

  it("ContentConfig has pages wrapper", () => {
    const c: ContentConfig = {
      pages: {
        home: {
          sections: {
            hero: { enabled: true, data: { headline: "H", tagline: "T", cta_text: "C", cta_link: "#" } },
          },
        },
      },
    };
    expect(c.pages.home.sections.hero?.enabled).toBe(true);
  });

  it("PageSections uses new section names", () => {
    const s: PageSections = {
      features: { enabled: true, data: { items: [{ title: "A", description: "B" }] } },
      gallery: { enabled: true, data: { items: [{ address: "1", city: "C", state: "S", price: "$1" }] } },
      contact_form: { enabled: true, data: { title: "T", subtitle: "S" } },
      profiles: { enabled: false, data: { items: [] } },
    };
    expect(s.features?.enabled).toBe(true);
    expect(s.gallery?.enabled).toBe(true);
    expect(s.contact_form?.enabled).toBe(true);
    expect(s.profiles?.enabled).toBe(false);
  });
});
