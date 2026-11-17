// Turns the two backgrounds of the Encounter template (bg-left.png for the left pages, even numbers;
// bg-right.png for the right pages, odd numbers: 14 MB lossless each, copied as they are into every
// PDF) into two JPEGs for the printed pages, print-background-left.jpg and print-background-right.jpg.
// Needs the API running. Usage: node scripts/flatten-print-background.mjs [out folder] [width] [quality] [--api http://localhost:5145]
import { chromium } from '@playwright/test'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
const args = process.argv.slice(2)
const at = args.indexOf('--api')
const api = at >= 0 ? args.splice(at, 2)[1] : 'http://localhost:5145'
const [out = '../../assets/templates/encounter', width = '1700', quality = '0.72'] = args
const browser = await chromium.launch({ channel: 'msedge' })
const page = await browser.newPage()
await page.goto(`${api}/api/packs`)
for (const side of ['left', 'right']) {
  const data = await page.evaluate(
    async ({ side, width, quality }) => {
      const img = new Image()
      img.src = `/api/assets/templates/encounter/bg-${side}.png`
      await img.decode()
      const w = Number(width),
        h = Math.round((w * img.naturalHeight) / img.naturalWidth)
      const c = document.createElement('canvas')
      c.width = w
      c.height = h
      const g = c.getContext('2d')
      g.imageSmoothingQuality = 'high'
      // The paper colour under what the layer leaves transparent
      g.fillStyle = '#d8c6a4'
      g.fillRect(0, 0, w, h)
      g.drawImage(img, 0, 0, w, h)
      return c.toDataURL('image/jpeg', Number(quality))
    },
    { side, width, quality },
  )
  writeFileSync(join(out, `print-background-${side}.jpg`), Buffer.from(data.split(',')[1], 'base64'))
}
await browser.close()
