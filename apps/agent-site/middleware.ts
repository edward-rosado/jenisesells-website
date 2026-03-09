import { NextRequest, NextResponse } from "next/server";
import { extractAgentId } from "./lib/routing";

export function middleware(request: NextRequest) {
  const hostname = request.headers.get("host") || "localhost:3000";
  const agentId = extractAgentId(hostname);

  if (agentId) {
    // Rewrite the URL to include agent ID for the page to access
    const url = request.nextUrl.clone();
    url.searchParams.set("agentId", agentId);
    const response = NextResponse.rewrite(url);
    response.headers.set("x-agent-id", agentId);
    return response;
  }

  // No agent subdomain — show landing/404
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next|favicon.ico|api).*)"],
};
