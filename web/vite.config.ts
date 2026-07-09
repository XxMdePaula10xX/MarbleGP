import { defineConfig } from 'vite';

// base './' — obrigatório para o Capacitor carregar via file:// no iOS.
export default defineConfig({
  base: './',
  build: {
    target: 'es2020',
    outDir: 'dist',
    assetsInlineLimit: 8192,
  },
  server: {
    host: true,
    port: 5173,
  },
});
