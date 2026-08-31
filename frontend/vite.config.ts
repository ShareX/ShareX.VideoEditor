import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// Vite config for ShareX Video Editor WebUI.
// Base is './' so all asset URLs are relative — required when loaded via file:// in Photino.
export default defineConfig({
  base: './',
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    restoreMocks: true,
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    rolldownOptions: {
      checks: {
        pluginTimings: false,
      },
    },
  },
})
