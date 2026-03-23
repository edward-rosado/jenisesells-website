import { createApiClient } from "@real-estate-star/api-client";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5135";

export const api = createApiClient(API_URL);
