// Flattens the two background layers of the Encounter template (bg-left.png, bg-right.png: 14 MB
// lossless each, copied as they are into every PDF) into one JPEG for the printed pages.
// Needs the API running. Usage: node scripts/flatten-print-background.mjs [out.jpg] [width] [quality] [--api http://localhost:5145]
import { chromium } from '@playwright/test'
import { writeFileSync } from 'node:fs'
const args = process.argv.slice(2)
const at = args.indexOf('--api')
const api = at >= 0 ? args.splice(at, 2)[1] : 'http://localhost:5145'
const [out = '../../assets/templates/encounter/print-background.jpg', width = '2550', quality = '0.82'] = args
const browser = await chromium.launch({ channel: 'msedge' })
const page = await browser.newPage()
await page.goto(`${api}/api/packs`)
const data = await page.evaluate(
  async ({ width, quality }) => {
    const load = (src) =>
      new Promise((ok, ko) => {
        const i = new Image()
        i.onload = () => ok(i)
        i.onerror = ko
        i.src = src
      })
    const base = '/api/assets/templates/encounter/'
    const [left, right] = await Promise.all([load(base + 'bg-left.png'), load(base + 'bg-right.png')])
    const w = Number(width),
      h = Math.round((w * left.naturalHeight) / left.naturalWidth)
    const c = document.createElement('canvas')
    c.width = w
    c.height = h
    const g = c.getContext('2d')
    g.imageSmoothingQuality = 'high'
    g.fillStyle = '#d8c6a4'
    g.fillRect(0, 0, w, h)
    g.drawImage(left, 0, 0, w, h)
    g.drawImage(right, 0, 0, w, h)
    return c.toDataURL('image/jpeg', Number(quality))
  },
  { width, quality },
)
writeFileSync(out, Buffer.from(data.split(',')[1], 'base64'))
await browser.close()
