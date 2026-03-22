import { describe, it, expect } from 'vitest';
import { createCorrelationId } from '../src/correlation';

describe('createCorrelationId', () => {
  it('returns a valid UUID v4 string', () => {
    const id = createCorrelationId();
    expect(id).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });

  it('returns unique values', () => {
    const ids = new Set(Array.from({ length: 100 }, () => createCorrelationId()));
    expect(ids.size).toBe(100);
  });

  it('is compatible with backend CorrelationIdMiddleware validation', () => {
    const id = createCorrelationId();
    expect(id.length).toBe(36);
    expect(id.length).toBeLessThanOrEqual(64);
    expect(id).toMatch(/^[a-zA-Z0-9_-]+$/);
  });
});
