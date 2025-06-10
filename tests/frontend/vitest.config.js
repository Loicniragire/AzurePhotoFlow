import { defineConfig } from 'vitest/config';
import path from 'path';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@frontend': path.resolve(__dirname, '../../frontend/src'),
      react: path.resolve(__dirname, 'node_modules/react'),
      'react-dom': path.resolve(__dirname, 'node_modules/react-dom'),
      jszip: path.resolve(__dirname, 'node_modules/jszip'),
      axios: path.resolve(__dirname, 'node_modules/axios'),
    },
  },
  test: {
    globals: true,         // Enables global test functions like `describe`, `test`, etc.
    environment: 'jsdom',  // Simulates browser-like environment for React
    setupFiles: path.resolve(__dirname, 'vitest.setup.js'), // Optional: Custom setup file for test utilities
    server: {
      fs: {
        allow: [path.resolve(__dirname, '../../frontend')],
      },
    },
  },
});

