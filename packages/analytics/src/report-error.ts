/**
 * Framework-agnostic error reporter using dependency injection.
 *
 * Each app wires in its own reporter (e.g. Sentry) via setErrorReporter in its layout.
 * This package does NOT import @sentry/nextjs — keeping it framework-agnostic.
 */
export type ErrorReporter = (
  error: unknown,
  context?: Record<string, string>
) => void;

let reporter: ErrorReporter = (error) => console.error(error);

/**
 * Replace the default console.error reporter with a custom implementation.
 * Call this once during app initialization (e.g. in layout.tsx).
 */
export function setErrorReporter(fn: ErrorReporter): void {
  reporter = fn;
}

/**
 * Report an error using the currently registered reporter.
 */
export function reportError(
  error: unknown,
  context?: Record<string, string>
): void {
  reporter(error, context);
}
