import { fileURLToPath, URL } from 'node:url'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

// The .NET API (taleweaver/src/Taleweaver.Api) listens on 5145; the dev server proxies /api to it.
const apiUrl = process.env.TALEWEAVER_API_URL ?? 'http://localhost:5145'

export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    // Every interface: the players' phones of the same Wi-Fi reach the room (the API refuses them the GM's routes).
    // TALEWEAVER_WEB_HOST=localhost keeps the app on this machine only.
    host: process.env.TALEWEAVER_WEB_HOST ?? true,
    port: Number(process.env.TALEWEAVER_WEB_PORT ?? 5173),
    // xfwd: the API tells the GM's machine from a phone by X-Forwarded-For; changeOrigin: the API sees its own host.
    proxy: {
      '/api': { target: apiUrl, xfwd: true, changeOrigin: true },
      '/hubs': { target: apiUrl, xfwd: true, ws: true, changeOrigin: true },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.ts'],
  },
})
