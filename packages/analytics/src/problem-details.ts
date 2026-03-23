/**
 * RFC 7807 Problem Details for HTTP APIs.
 * @see https://datatracker.ietf.org/doc/html/rfc7807
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
}

/**
 * Parses an RFC 7807 problem+json response body.
 * Returns null if the response is not a problem+json content type.
 */
export async function parseProblemDetails(
  response: Response
): Promise<ProblemDetails | null> {
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/problem+json")) return null;
  return response.json() as Promise<ProblemDetails>;
}
