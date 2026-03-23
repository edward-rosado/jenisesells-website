// @vitest-environment jsdom
import { describe, it, expect } from "vitest";
import type { SoldHomeItem, ServiceItem } from "@/features/config/types";

describe("SoldHomeItem type extensions", () => {
  it("accepts optional commercial fields", () => {
    const item: SoldHomeItem = {
      address: "100 Main St",
      city: "Dallas",
      state: "TX",
      price: "$4,200,000",
      property_type: "Office",
      sq_ft: "45,000 SF",
      cap_rate: "6.2%",
      noi: "$280,000",
      badge_label: "CLOSED",
      features: [{ label: "Lot", value: "5 acres" }],
      client_quote: "We found our forever home!",
      client_name: "The Kim Family",
      tags: ["Oceanfront", "Beach Access"],
    };
    expect(item.property_type).toBe("Office");
    expect(item.features?.[0].label).toBe("Lot");
    expect(item.client_quote).toBe("We found our forever home!");
    expect(item.tags).toEqual(["Oceanfront", "Beach Access"]);
  });
});

describe("ServiceItem type extensions", () => {
  it("accepts optional category field", () => {
    const item: ServiceItem = {
      title: "Tenant Rep",
      description: "Finding the right space",
      category: "Services",
    };
    expect(item.category).toBe("Services");
  });
});
