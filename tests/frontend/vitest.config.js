import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globals: true,         // Enables global test functions like `describe`, `test`, etc.
    environment: 'jsdom',  // Simulates browser-like environment for React
    setupFiles: './vitest.setup.js', // Optional: Custom setup file for test utilities
  },
});

