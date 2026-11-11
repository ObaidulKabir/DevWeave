import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// The dev server proxies control-plane traffic to DevWeave.Api so the studio can call the API
// without CORS configuration (see src/DevWeave.Api/Properties/launchSettings.json for the port).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: '127.0.0.1',
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
      '/health': { target: 'http://localhost:5080', changeOrigin: true },
      '/tenants': { target: 'http://localhost:5080', changeOrigin: true },
      '/schemas': { target: 'http://localhost:5080', changeOrigin: true },
      '/relation-types': { target: 'http://localhost:5080', changeOrigin: true },
    },
  },
});
