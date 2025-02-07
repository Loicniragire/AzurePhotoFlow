import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  // Load environment variables from the current working directory.
  // The empty string as the third parameter means we load all variables without any prefix filtering.
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react()],
    define: {
      // Explicitly define the API base URL from the environment variable.
      // Ensure that VITE_API_BASE_URL is defined in your .env file or in your process environment.
      'import.meta.env.VITE_API_BASE_URL': JSON.stringify(env.VITE_API_BASE_URL)
    }
  };
});
