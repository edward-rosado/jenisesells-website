/** Mirrors .NET SubmitCmaRequest. beds/baths/sqft are integers (whole numbers only). */
export interface CmaSubmitRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  address: string;
  city: string;
  state: string;
  zip: string;
  timeline: string;
  beds?: number;
  baths?: number;
  sqft?: number;
  notes?: string;
}

/** Mirrors .NET SubmitCmaResponse */
export interface CmaSubmitResponse {
  jobId: string;
  status: string;
}

/**
 * Mirrors .NET GetStatusResponse / SignalR StatusUpdate.
 * Used by apps/portal for progress tracking.
 */
export interface CmaStatusUpdate {
  status: string;
  step: number;
  totalSteps: number;
  message: string;
  errorMessage?: string | null;
}
