import { describe, it, expect } from 'vitest';
import { readFileSync } from 'fs';
import { join } from 'path';

const ALLOWED_DEPS: Record<string, string[]> = {
  'domain': [],
  'api-client': ['domain'],
  'forms': ['domain'],
  'legal': ['domain'],
  'analytics': ['domain'],
};

describe('Frontend Architecture', () => {
  for (const [pkg, allowed] of Object.entries(ALLOWED_DEPS)) {
    it(`packages/${pkg} only depends on allowed packages`, () => {
      const pkgJsonPath = join(__dirname, '../../..', 'packages', pkg, 'package.json');
      const pkgJson = JSON.parse(readFileSync(pkgJsonPath, 'utf-8'));
      const deps = { ...pkgJson.dependencies, ...pkgJson.devDependencies };
      const internalDeps = Object.keys(deps)
        .filter(d => d.startsWith('@real-estate-star/'))
        .map(d => d.replace('@real-estate-star/', ''));

      for (const dep of internalDeps) {
        expect(allowed, `packages/${pkg} must not depend on @real-estate-star/${dep}`).toContain(dep);
      }
    });
  }
});
