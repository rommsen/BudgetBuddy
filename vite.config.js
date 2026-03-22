import { defineConfig } from 'vite';
import fable from 'vite-plugin-fable';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [
    fable({
      fsproj: './src/Client/Client.fsproj',
      babel: {
        plugins: []
      }
    }),
    tailwindcss()
  ],
  root: './src/Client',
  server: {
    port: 5181,
    proxy: {
      '/api': {
        target: 'http://localhost:5081',
        changeOrigin: true
      }
    }
  },
  build: {
    outDir: '../../dist/public',
    emptyOutDir: true
  }
});
