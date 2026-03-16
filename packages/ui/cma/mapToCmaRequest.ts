import type { LeadFormData, CmaSubmitRequest } from "@real-estate-star/shared-types";

/**
 * Maps the form's LeadFormData shape to the flat CmaSubmitRequest
 * the .NET API expects. Rounds beds/baths/sqft to integers since
 * the API uses int? (not float).
 *
 * When seller data is absent (buyer-only lead), address fields
 * default to empty strings. The consuming app should guard against
 * submitting buyer-only leads to the CMA endpoint.
 */
export function mapToCmaRequest(data: LeadFormData): CmaSubmitRequest {
  return {
    firstName: data.firstName,
    lastName: data.lastName,
    email: data.email,
    phone: data.phone,
    address: data.seller?.address ?? "",
    city: data.seller?.city ?? "",
    state: data.seller?.state ?? "",
    zip: data.seller?.zip ?? "",
    timeline: data.timeline,
    beds: data.seller?.beds != null ? Math.round(data.seller.beds) : undefined,
    baths: data.seller?.baths != null ? Math.round(data.seller.baths) : undefined,
    sqft: data.seller?.sqft != null ? Math.round(data.seller.sqft) : undefined,
    notes: data.notes,
  };
}
