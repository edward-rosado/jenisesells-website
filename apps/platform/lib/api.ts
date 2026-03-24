import { createApiClient } from "@real-estate-star/api-client";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";

/** Create a fresh API client per request. Each call gets a unique X-Correlation-ID. */
export function getApi() {
  return createApiClient(API_URL);
}

/** @deprecated Use getApi() for per-request correlation IDs */
export const api = createApiClient(API_URL);
