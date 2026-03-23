import { describe, it, expect } from "vitest";
import { parseProblemDetails } from "../src/problem-details";
import type { ProblemDetails } from "../src/problem-details";

function makeResponse(body: unknown, contentType: string): Response {
  return new Response(JSON.stringify(body), {
    status: 400,
    headers: { "Content-Type": contentType },
  });
}

describe("parseProblemDetails", () => {
  it("parses a valid problem+json response", async () => {
    const body: ProblemDetails = {
      type: "https://example.com/probs/validation",
      title: "Validation Error",
      status: 422,
      detail: "The 'email' field is required.",
    };
    const response = makeResponse(body, "application/problem+json");

    const result = await parseProblemDetails(response);

    expect(result).toEqual(body);
  });

  it("returns null for application/json content-type", async () => {
    const response = makeResponse({ error: "bad request" }, "application/json");

    const result = await parseProblemDetails(response);

    expect(result).toBeNull();
  });

  it("returns null for text/html content-type", async () => {
    const response = new Response("<html>Not Found</html>", {
      status: 404,
      headers: { "Content-Type": "text/html" },
    });

    const result = await parseProblemDetails(response);

    expect(result).toBeNull();
  });

  it("returns null when content-type header is absent", async () => {
    const response = new Response(null, { status: 500 });

    const result = await parseProblemDetails(response);

    expect(result).toBeNull();
  });

  it("parses problem+json with charset suffix in content-type", async () => {
    const body: ProblemDetails = {
      title: "Conflict",
      status: 409,
    };
    const response = makeResponse(body, "application/problem+json; charset=utf-8");

    const result = await parseProblemDetails(response);

    expect(result).toEqual(body);
  });

  it("handles partial problem+json with only some fields present", async () => {
    const body: ProblemDetails = { title: "Internal Server Error", status: 500 };
    const response = makeResponse(body, "application/problem+json");

    const result = await parseProblemDetails(response);

    expect(result?.title).toBe("Internal Server Error");
    expect(result?.status).toBe(500);
    expect(result?.detail).toBeUndefined();
    expect(result?.type).toBeUndefined();
  });
});
