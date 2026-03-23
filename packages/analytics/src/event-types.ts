/**
 * Analytics event types matching the backend FormEvent enum (PascalCase via JsonStringEnumConverter).
 *
 * IMPORTANT: The backend serializes enums as PascalCase strings ("Viewed", not "form.viewed").
 * Using this enum ensures the frontend and backend remain in sync.
 */
export enum EventType {
  Viewed = "Viewed",
  Started = "Started",
  Submitted = "Submitted",
  Succeeded = "Succeeded",
  Failed = "Failed",
}
