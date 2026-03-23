// scripts/validate-architecture.mjs
// Validates that frontend packages only depend on their allowed internal packages.
// Run: node scripts/validate-architecture.mjs
import { readFileSync } from 'fs';
import { join } from 'path';

const ALLOWED_DEPS = {
  'domain': [],
  'api-client': ['domain'],
  'forms': ['domain'],
  'legal': ['domain'],
  'analytics': ['domain'],
};

const packagesDir = join(process.cwd(), 'packages');
let hasErrors = false;

for (const [pkg, allowed] of Object.entries(ALLOWED_DEPS)) {
  const pkgJsonPath = join(packagesDir, pkg, 'package.json');
  try {
    const pkgJson = JSON.parse(readFileSync(pkgJsonPath, 'utf-8'));
    const deps = { ...pkgJson.dependencies, ...pkgJson.devDependencies };
    const internalDeps = Object.keys(deps)
      .filter(d => d.startsWith('@real-estate-star/'))
      .map(d => d.replace('@real-estate-star/', ''));

    for (const dep of internalDeps) {
      if (!allowed.includes(dep)) {
        console.error(`ERROR: packages/${pkg} depends on @real-estate-star/${dep} (not allowed)`);
        hasErrors = true;
      }
    }
  } catch (e) {
    console.error(`ERROR: Could not read packages/${pkg}/package.json`);
    hasErrors = true;
  }
}

if (hasErrors) {
  console.error('\nArchitecture validation FAILED');
  process.exit(1);
} else {
  console.log('Architecture validation passed');
}
