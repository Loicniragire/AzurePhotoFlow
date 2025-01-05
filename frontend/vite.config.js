import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  build: {
    outDir: 'build',
  },
  server: {
    host: '0.0.0.0', // Allows access from Docker or other devices
    port: 3000,
  },
  base: mode === 'production' ? '/production/' : '/', // Example conditional base URL
}));

