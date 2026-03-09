import * as Sentry from "@sentry/nextjs";

Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
  enabled: !!process.env.NEXT_PUBLIC_SENTRY_DSN,

  // Performance: capture 100% of transactions at MVP traffic levels
  tracesSampleRate: 1.0,

  // Session Replay: only on errors (PII-safe for real estate lead forms)
  replaysSessionSampleRate: 0,
  replaysOnErrorSampleRate: 1.0,

  // Filter noisy errors
  ignoreErrors: [
    "ResizeObserver loop",
    "Non-Error promise rejection",
  ],
});
