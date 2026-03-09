"use client";

import * as Sentry from "@sentry/nextjs";
import { useEffect } from "react";

interface GlobalErrorProps {
  error: Error & { digest?: string };
  reset: () => void;
}

export default function GlobalError({ error, reset }: GlobalErrorProps) {
  useEffect(() => {
    Sentry.captureException(error);
  }, [error]);

  return (
    <html lang="en">
      <body>
        <main className="min-h-screen flex items-center justify-center">
          <div className="text-center max-w-md px-6">
            <h1 className="text-4xl font-bold mb-4">Something went wrong</h1>
            <p className="text-gray-500 mb-6">
              We encountered an unexpected error. Please try refreshing the page.
            </p>
            <button
              onClick={reset}
              className="px-6 py-3 bg-gray-900 text-white rounded-lg font-medium"
            >
              Try again
            </button>
          </div>
        </main>
      </body>
    </html>
  );
}
