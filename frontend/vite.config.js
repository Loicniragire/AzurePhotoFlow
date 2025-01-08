import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  build: {
    outDir: 'build', // Output directory
    assetsDir: 'assets', // Place assets in 'build/assets/'
    emptyOutDir: true, // Clean output directory before each build
  },
  server: {
    host: '0.0.0.0', // Allows access from Docker or other devices
    port: 3000,
  },
  base: mode === 'production' ? '/' : '/', // Keep base URL consistent
}));

