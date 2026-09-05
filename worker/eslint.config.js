// @ts-check
import tseslint from "@typescript-eslint/eslint-plugin";
import tsParser from "@typescript-eslint/parser";

export default [
  {
    ignores: ["dist/**", "node_modules/**", ".wrangler/**"],
  },
  {
    files: ["src/**/*.ts", "test/**/*.ts"],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        project: "./tsconfig.json",
      },
    },
    plugins: {
      "@typescript-eslint": tseslint,
    },
    rules: {
      ...tseslint.configs.recommended.rules,
      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_" }],
      "@typescript-eslint/consistent-type-imports": "error",
      "no-console": ["error", { allow: ["error"] }],
    },
  },
  {
    files: ["test/**/*.ts"],
    rules: {
      // Test helpers pass through arbitrary JSON response bodies from many
      // different endpoints; typing each shape here would just duplicate
      // the route handlers' own types for no safety benefit.
      "@typescript-eslint/no-explicit-any": "off",
    },
  },
];
