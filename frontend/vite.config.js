import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  build: {
    outDir: 'build',
    assetsDir: 'assets',
    emptyOutDir: true,
  },
  server: {
    host: '0.0.0.0',
    port: 3000,
    // Proxy API requests to Nginx (development only)
    proxy: {
      '/api': {
        target: 'http://nginx:80', // Docker service name for Nginx
        changeOrigin: true,
	    secure: false,
		rewrite: (path) => path.replace(/^\/api/, '') 
      }
    }
  },
  // Production configuration
  base: mode === 'production' ? '/' : '/',
  define: {
    'process.env.VITE_API_BASE_URL': JSON.stringify(
      mode === 'production' ? '/api' : '/api' // Use relative path in production
    )
  }
}));
