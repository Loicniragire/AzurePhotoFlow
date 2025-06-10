import { defineConfig } from 'vitest/config';
import path from 'path';

export default defineConfig({
  resolve: {
    alias: {
      '@frontend': path.resolve(__dirname, '../../frontend/src'),
    },
  },
  test: {
    globals: true,         // Enables global test functions like `describe`, `test`, etc.
    environment: 'jsdom',  // Simulates browser-like environment for React
    setupFiles: './vitest.setup.js', // Optional: Custom setup file for test utilities
  },
});

