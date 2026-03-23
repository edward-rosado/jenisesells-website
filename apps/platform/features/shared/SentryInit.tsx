"use client";

import { useEffect } from "react";
import { setErrorReporter } from "@real-estate-star/analytics";
import * as Sentry from "@sentry/nextjs";

/**
 * Client component that wires the analytics error reporter to Sentry.
 * Must run in the browser — layout.tsx is a Server Component so this
 * is extracted as a thin "use client" boundary.
 * Renders nothing visible — side-effects only.
 */
export function SentryInit() {
  useEffect(() => {
    setErrorReporter((error, context) =>
      Sentry.captureException(error, { extra: context })
    );
  }, []);

  return null;
}
