import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "path";

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./vitest.setup.ts"],
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov", "html"],
      include: ["lib/**/*.ts", "components/**/*.tsx", "app/**/*.tsx", "features/**/*.tsx", "features/**/*.ts"],
      exclude: ["**/__tests__/**", "**/*.d.ts", "features/**/index.ts"],
      thresholds: {
        branches: 100,
        functions: 100,
        lines: 100,
        statements: 100,
      },
    },
  },
  css: {
    // Disable PostCSS processing in tests — avoids Tailwind v4 native binding
    // issues on CI where optional deps may not resolve correctly (npm#4828)
    postcss: {},
  },
  resolve: {
    alias: { "@": path.resolve(__dirname, ".") },
  },
});
