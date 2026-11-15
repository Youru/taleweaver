import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from '@playwright/test'

// End-to-end: the real API on the repository content (saves in a temporary folder) and the Vite dev server.
const apiPort = 5146
const webPort = 5174
const saves = mkdtempSync(join(tmpdir(), 'taleweaver-e2e-'))
const repositoryRoot = fileURLToPath(new URL('../..', import.meta.url))

export default defineConfig({
  testDir: 'e2e',
  fullyParallel: false,
  workers: 1,
  use: { baseURL: `http://localhost:${webPort}`, channel: 'msedge', locale: 'fr-FR' },
  webServer: [
    {
      command: `dotnet run --project ../src/Taleweaver.Api --no-launch-profile --urls http://localhost:${apiPort}`,
      url: `http://localhost:${apiPort}/api/packs`,
      env: { ASPNETCORE_ENVIRONMENT: 'Development', Content__RootPath: repositoryRoot, Content__SavesPath: saves },
      timeout: 120_000,
      reuseExistingServer: false,
    },
    {
      command: 'npx vite',
      url: `http://localhost:${webPort}`,
      env: { TALEWEAVER_API_URL: `http://localhost:${apiPort}`, TALEWEAVER_WEB_PORT: String(webPort) },
      reuseExistingServer: false,
    },
  ],
})
