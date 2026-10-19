import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, lazyPlugins } from "vite-plus";

// https://vite.dev/config/
export default defineConfig({
  fmt: {},
  lint: {
    plugins: ["react", "typescript", "oxc"],
    rules: {
      "react/rules-of-hooks": "error",
      "react/only-export-components": [
        "warn",
        {
          allowConstantExport: true,
        },
      ],
      "vite-plus/prefer-vite-plus-imports": "error",
      // AI Elements is copied in, but we don't depend on the Vercel AI SDK: use the local types.
      "no-restricted-imports": [
        "error",
        {
          paths: [
            {
              name: "ai",
              message: "Use src/components/ai-elements/types.ts instead of the Vercel AI SDK.",
            },
          ],
        },
      ],
    },
    overrides: [
      {
        // Copied from shadcn/ui and AI Elements; kept as close to upstream as possible.
        files: ["src/components/ui/**", "src/components/ai-elements/**"],
        rules: {
          "react/only-export-components": "off",
          "react/refs": "off",
          "react/static-components": "off",
          "typescript/no-floating-promises": "off",
          "typescript/restrict-template-expressions": "off",
        },
      },
    ],
    options: {
      typeAware: true,
      typeCheck: true,
    },
    jsPlugins: [
      {
        name: "vite-plus",
        specifier: "vite-plus/oxlint-plugin",
      },
    ],
  },
  plugins: lazyPlugins(() => [react(), tailwindcss()]),
  resolve: {
    alias: { "@": path.resolve(import.meta.dirname, "src") },
  },
  server: {
    proxy: {
      // Aspire injects the API's URL; in production the API serves this app from the same origin.
      "/api": {
        target: process.env.API_HTTPS ?? process.env.API_HTTP,
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
