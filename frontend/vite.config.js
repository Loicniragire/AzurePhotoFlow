import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  const isProd = mode === 'production';

  return {
    plugins: [react()],

    build: {
      outDir: 'dist',
      assetsDir: 'assets',
      emptyOutDir: true,
    },

    server: {
      host: '0.0.0.0',
      port: 3000,
      proxy: {
        '/api': {
          target: 'http://nginx:80',
          changeOrigin: true,
          secure: false,
          rewrite: (path) => path.replace(/^\/api/, ''),
        },
      },
    },

    base: isProd ? '/' : '/',

    // If your code references `import.meta.env.VITE_API_BASE_URL`:
    define: {
      'import.meta.env.VITE_API_BASE_URL': JSON.stringify(
        isProd
          ? 'https://myprodbackend.azurewebsites.net/api'
          : 'http://localhost:3000/api'
      ),
    },
  };
});

