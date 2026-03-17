import { describe, it, expect } from "vitest";
import { mapToCmaRequest } from "./mapToCmaRequest";
import type { LeadFormData } from "@real-estate-star/shared-types";

function makeSellerLead(overrides?: Partial<LeadFormData>): LeadFormData {
  return {
    leadTypes: ["selling"],
    firstName: "Jane",
    lastName: "Doe",
    email: "jane@example.com",
    phone: "555-0100",
    seller: {
      address: "123 Main St",
      city: "Newark",
      state: "NJ",
      zip: "07101",
      beds: 3,
      baths: 2,
      sqft: 1800,
    },
    timeline: "asap",
    ...overrides,
  };
}

describe("mapToCmaRequest", () => {
  it("extracts seller fields into flat request", () => {
    const result = mapToCmaRequest(makeSellerLead());

    expect(result).toEqual({
      firstName: "Jane",
      lastName: "Doe",
      email: "jane@example.com",
      phone: "555-0100",
      address: "123 Main St",
      city: "Newark",
      state: "NJ",
      zip: "07101",
      timeline: "asap",
      beds: 3,
      baths: 2,
      sqft: 1800,
      notes: undefined,
    });
  });

  it("includes notes when present", () => {
    const result = mapToCmaRequest(makeSellerLead({ notes: "Corner lot" }));
    expect(result.notes).toBe("Corner lot");
  });

  it("defaults address fields to empty strings for buyer-only leads", () => {
    const buyerOnly: LeadFormData = {
      leadTypes: ["buying"],
      firstName: "Bob",
      lastName: "Smith",
      email: "bob@example.com",
      phone: "555-0200",
      buyer: {
        desiredArea: "Downtown",
        minPrice: 200000,
        maxPrice: 400000,
      },
      timeline: "3-6months",
    };

    const result = mapToCmaRequest(buyerOnly);

    expect(result.address).toBe("");
    expect(result.city).toBe("");
    expect(result.state).toBe("");
    expect(result.zip).toBe("");
    expect(result.beds).toBeUndefined();
    expect(result.baths).toBeUndefined();
    expect(result.sqft).toBeUndefined();
  });

  it("rounds beds/baths/sqft to integers", () => {
    const result = mapToCmaRequest(
      makeSellerLead({
        seller: {
          address: "1 Oak Ave",
          city: "Trenton",
          state: "NJ",
          zip: "08601",
          beds: 3.7,
          baths: 2.5,
          sqft: 1899.9,
        },
      }),
    );

    expect(result.beds).toBe(4);
    expect(result.baths).toBe(3); // Math.round(2.5) = 3 (banker's rounding in some engines, but Math.round always rounds .5 up)
    expect(result.sqft).toBe(1900);
  });

  it("leaves beds/baths/sqft undefined when not provided", () => {
    const result = mapToCmaRequest(
      makeSellerLead({
        seller: {
          address: "1 Oak Ave",
          city: "Trenton",
          state: "NJ",
          zip: "08601",
        },
      }),
    );

    expect(result.beds).toBeUndefined();
    expect(result.baths).toBeUndefined();
    expect(result.sqft).toBeUndefined();
  });

  it("preserves timeline value", () => {
    const result = mapToCmaRequest(makeSellerLead({ timeline: "6-12months" }));
    expect(result.timeline).toBe("6-12months");
  });
});
