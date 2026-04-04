import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

// Feature folders: config, templates, sections, lead-capture, privacy, shared, i18n
//
// Cross-feature import rules:
//   shared/        → anyone may import (no restrictions)
//   shared/        → config/ ALLOWED
//   i18n/          → pure utility — config/ ALLOWED, nothing else
//   sections/      → config/ ALLOWED, shared/ ALLOWED, i18n/ ALLOWED
//   sections/      → shared/ ALLOWED
//   templates/     → config/ ALLOWED, i18n/ ALLOWED
//   templates/     → sections/{barrel} ALLOWED (subsection barrels only, NOT top-level index)
//   lead-capture/  → config/ ALLOWED
//   lead-capture/  → shared/ ALLOWED
//   privacy/       → shared/ ALLOWED
//   ALL other cross-feature imports are BLOCKED

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
    // Generated coverage output:
    "coverage/**",
  ]),

  // ── i18n/ ──────────────────────────────────────────────────────────────────
  // i18n/ is a pure utility — it may import config/ only. No other features.
  {
    files: ["features/i18n/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          {
            group: ["**/features/templates/**", "**/features/sections/**", "**/features/lead-capture/**", "**/features/privacy/**", "**/features/shared/**"],
            message: "i18n/ is a pure utility — it must not import from other feature folders. Only config/ is allowed.",
          },
        ],
      }],
    },
  },

  // ── config/ ────────────────────────────────────────────────────────────────
  // config is the base — it imports nothing from other features.
  {
    files: ["features/config/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          {
            group: ["**/features/templates/**", "**/features/sections/**", "**/features/lead-capture/**", "**/features/privacy/**", "**/features/shared/**"],
            message: "config/ must not import from other feature folders.",
          },
        ],
      }],
    },
  },

  // ── shared/ ────────────────────────────────────────────────────────────────
  // shared/ may import config/ (for branding/routing) but nothing else.
  {
    files: ["features/shared/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          {
            group: ["**/features/templates/**", "**/features/sections/**", "**/features/lead-capture/**", "**/features/privacy/**"],
            message: "shared/ must not import from templates/, sections/, lead-capture/, or privacy/. shared/ may only import from config/.",
          },
        ],
      }],
    },
  },

  // ── sections/ ──────────────────────────────────────────────────────────────
  // sections/ may import config/ and shared/. It must NOT import templates/, lead-capture/, or privacy/.
  // Exception: sections/shared/CmaSection.tsx may import lead-capture/submit-lead (intentional coupling —
  // CmaSection is the CMA form UI and submitting leads is its sole purpose).
  {
    files: ["features/sections/**"],
    ignores: ["features/sections/shared/CmaSection.tsx"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          {
            group: ["**/features/templates/**"],
            message: "sections/ must not import from templates/.",
          },
          {
            group: ["**/features/lead-capture/**"],
            message: "sections/ must not import from lead-capture/. Move shared utilities (e.g. safe-contact) to shared/ instead.",
          },
          {
            group: ["**/features/privacy/**"],
            message: "sections/ must not import from privacy/.",
          },
        ],
      }],
    },
  },

  // ── templates/ ─────────────────────────────────────────────────────────────
  // templates/ may import config/, shared/, and sections/{barrel} (subsection barrels only).
  // templates/ must NOT import the top-level sections/index.ts barrel.
  {
    files: ["features/templates/**"],
    rules: {
      "no-restricted-imports": ["error", {
        // Use `paths` (exact name match) to block the top-level sections barrel without
        // accidentally blocking subsection barrels like @/features/sections/heroes.
        paths: [
          {
            name: "@/features/sections",
            message: "templates/ must import from subsection barrels (e.g. @/features/sections/heroes), not the top-level sections barrel.",
          },
          {
            name: "@/features/sections/index",
            message: "templates/ must import from subsection barrels (e.g. @/features/sections/heroes), not the top-level sections barrel.",
          },
        ],
        patterns: [
          {
            group: ["**/features/lead-capture/**"],
            message: "templates/ must not import from lead-capture/.",
          },
          {
            group: ["**/features/privacy/**"],
            message: "templates/ must not import from privacy/.",
          },
        ],
      }],
    },
  },

  // ── lead-capture/ ──────────────────────────────────────────────────────────
  // lead-capture/ may import config/ and shared/. It must NOT import templates/, sections/, or privacy/.
  {
    files: ["features/lead-capture/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          {
            group: ["**/features/templates/**"],
            message: "lead-capture/ must not import from templates/.",
          },
          {
            group: ["**/features/sections/**"],
            message: "lead-capture/ must not import from sections/.",
          },
          {
            group: ["**/features/privacy/**"],
            message: "lead-capture/ must not import from privacy/.",
          },
        ],
      }],
    },
  },

  // ── privacy/ ───────────────────────────────────────────────────────────────
  // privacy/ may import config/ and shared/. It must NOT import templates/, sections/, or lead-capture/.
  {
    files: ["features/privacy/**"],
    rules: {
      "no-restricted-imports": ["error", {
        patterns: [
          {
            group: ["**/features/templates/**"],
            message: "privacy/ must not import from templates/.",
          },
          {
            group: ["**/features/sections/**"],
            message: "privacy/ must not import from sections/.",
          },
          {
            group: ["**/features/lead-capture/**"],
            message: "privacy/ must not import from lead-capture/. Use @/features/shared/hmac for HMAC signing.",
          },
        ],
      }],
    },
  },
]);

export default eslintConfig;
