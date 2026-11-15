import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

// The .NET API (taleweaver/src/Taleweaver.Api) listens on 5145; the dev server proxies /api to it.
const apiUrl = process.env.TALEWEAVER_API_URL ?? 'http://localhost:5145'

export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    host: 'localhost',
    port: Number(process.env.TALEWEAVER_WEB_PORT ?? 5173),
    proxy: { '/api': apiUrl },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.ts'],
  },
})
