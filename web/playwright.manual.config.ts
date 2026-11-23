import { fileURLToPath } from 'node:url'
import type { PlaywrightTestConfig } from '@playwright/test'
import base from './playwright.config'

// The pictures of the manual (manual-shots/), taken on the demonstration content: nothing of the game ever shows.
// npm run manual:shots → public/help/*.png
const demo = fileURLToPath(new URL('../demo', import.meta.url))
const servers = Array.isArray(base.webServer) ? base.webServer : base.webServer ? [base.webServer] : []

export default {
  ...base,
  testDir: 'manual-shots',
  use: { ...base.use, viewport: { width: 1280, height: 800 } },
  webServer: servers.map((server) =>
    server.env && 'Content__RootPath' in server.env
      ? // A made-up address in the room's QR code: never the author's network
        { ...server, env: { ...server.env, Content__RootPath: demo, TALEWEAVER_ROOM_HOST: '192.168.1.20' } }
      : server,
  ),
} satisfies PlaywrightTestConfig
