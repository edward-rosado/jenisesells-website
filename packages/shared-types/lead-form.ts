export type LeadType = "buying" | "selling";

export type PreApprovalStatus = "yes" | "no" | "in-progress";

export type Timeline =
  | "asap"
  | "1-3months"
  | "3-6months"
  | "6-12months"
  | "justcurious";

export interface BuyerDetails {
  desiredArea: string;
  minPrice?: number;
  maxPrice?: number;
  minBeds?: number;
  minBaths?: number;
  preApproved?: PreApprovalStatus;
  preApprovalAmount?: number;
}

export interface SellerDetails {
  address: string;
  city: string;
  state: string;
  zip: string;
  beds?: number;
  baths?: number;
  sqft?: number;
}

export interface LeadFormData {
  leadTypes: LeadType[];
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  buyer?: BuyerDetails;
  seller?: SellerDetails;
  timeline: Timeline;
  notes?: string;
}
