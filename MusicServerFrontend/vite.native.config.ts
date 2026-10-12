import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// A separate entry keeps browser diagnostics and HTML audio out of the shell.
export default defineConfig({
  root: fileURLToPath(new URL('./native-shell', import.meta.url)),
  base: './',
  plugins: [react()],
  build: {
    outDir: fileURLToPath(new URL('./dist-native', import.meta.url)),
    emptyOutDir: true,
  },
});
