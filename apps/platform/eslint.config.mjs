import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    ".open-next/**",
    "out/**",
    "build/**",
    "coverage/**",
    "next-env.d.ts",
  ]),

  // Cross-feature import restrictions.
  // Features cannot import from other features; only features/shared/ is allowed as a cross-feature dependency.
  // All features may freely import from @real-estate-star/* packages.
  {
    files: ["apps/platform/features/onboarding/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          { group: ["**/features/billing", "**/features/billing/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/landing", "**/features/landing/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/status", "**/features/status/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
        ],
      }],
    },
  },
  {
    files: ["apps/platform/features/billing/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          { group: ["**/features/onboarding", "**/features/onboarding/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/landing", "**/features/landing/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/status", "**/features/status/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
        ],
      }],
    },
  },
  {
    files: ["apps/platform/features/landing/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          { group: ["**/features/onboarding", "**/features/onboarding/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/billing", "**/features/billing/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/status", "**/features/status/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
        ],
      }],
    },
  },
  {
    files: ["apps/platform/features/status/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          { group: ["**/features/onboarding", "**/features/onboarding/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/billing", "**/features/billing/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
          { group: ["**/features/landing", "**/features/landing/**"], message: "Features cannot cross-import. Use features/shared/ or a @real-estate-star/* package." },
        ],
      }],
    },
  },
  {
    // shared/ is a dependency provider, not a consumer — it cannot import from any feature.
    files: ["apps/platform/features/shared/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          { group: ["**/features/onboarding", "**/features/onboarding/**"], message: "features/shared/ cannot import from features. Extract to a @real-estate-star/* package if needed." },
          { group: ["**/features/billing", "**/features/billing/**"], message: "features/shared/ cannot import from features. Extract to a @real-estate-star/* package if needed." },
          { group: ["**/features/landing", "**/features/landing/**"], message: "features/shared/ cannot import from features. Extract to a @real-estate-star/* package if needed." },
          { group: ["**/features/status", "**/features/status/**"], message: "features/shared/ cannot import from features. Extract to a @real-estate-star/* package if needed." },
        ],
      }],
    },
  },
]);

export default eslintConfig;
