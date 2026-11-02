import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  publicDir: '../PreviewSamples',
  server: { proxy: { '/api': process.env.PRINTSHARP_API ?? 'http://127.0.0.1:5080' } }
});
