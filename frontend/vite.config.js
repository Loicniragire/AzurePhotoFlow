import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import history from 'connect-history-api-fallback';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'build',
  },
  server: {
    host: '0.0.0.0',
    port: 3000,
    middlewareMode: true, // Enable middleware
    setupMiddleware: (app) => {
      app.use(history()); // Redirect all navigation requests to index.html
    },
  },
  base: '/', // Base URL for the app
});

