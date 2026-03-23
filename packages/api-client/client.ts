import createClient from "openapi-fetch";
import type { paths } from "./generated/types";
import { createCorrelationId } from "@real-estate-star/domain";

/**
 * Create a typed API client. Static headers (e.g., bearer token) can be set
 * at construction time. Per-request headers (e.g., HMAC signature) should be
 * passed at the call site: client.POST("/path", { headers: { ... } }).
 */
export function createApiClient(baseUrl: string, headers?: Record<string, string>) {
  return createClient<paths>({
    baseUrl,
    headers: {
      "X-Correlation-ID": createCorrelationId(),
      ...headers,
    },
  });
}

export type ApiClient = ReturnType<typeof createApiClient>;
