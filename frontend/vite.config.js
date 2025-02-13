import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  // Load environment variables
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react()],
    resolve: {
      alias: { }
    },
    optimizeDeps: {
      include: ["jwt-decode"]
    },
    define: {
      'import.meta.env.VITE_API_BASE_URL': JSON.stringify(env.VITE_API_BASE_URL)
    }
  };
});

