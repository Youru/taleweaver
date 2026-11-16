// Measures the colour of every square of every tile, from its image, so that the checkers can tell
// whether two tiles laid side by side match (green moss against golden rock looks odd even when both
// are "dark-cave"). One letter a square, tile not rotated, as the terrain survey:
//   g green (moss, leaves)   y gold, ochre, brown (rock, earth, wood)   b blue (water, ice, crystal)
//   r red (blood, banners)   k grey or dark (stone, shadow, void)
// Usage (from taleweaver/web): npm run tile-palette [-- --print]
// Writes docs/extraction/tile-palette.txt; tools/build_components.py copies it into packs/um1/tiles.json.
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const tiles = JSON.parse(readFileSync(join(root, 'packs/um1/tiles.json'), 'utf8')).tiles

const browser = await chromium.launch({ channel: 'msedge' })
const page = await browser.newPage()

const lines = []
for (const tile of tiles) {
  const rows = await page.evaluate(
    async (src) => {
      const image = new Image()
      image.src = src
      await image.decode()
      const size = 600
      const canvas = document.createElement('canvas')
      canvas.width = canvas.height = size
      const context = canvas.getContext('2d')
      context.drawImage(image, 0, 0, size, size)
      const { data } = context.getImageData(0, 0, size, size)
      const cell = size / 6
      // The class of one pixel, from its hue, saturation and value
      const classOf = (r, g, b) => {
        const max = Math.max(r, g, b)
        const min = Math.min(r, g, b)
        const v = max / 255
        const s = max ? (max - min) / max : 0
        if (v < 0.16 || s < 0.28) return 'k'
        let h
        if (max === r) h = (60 * ((g - b) / (max - min)) + 360) % 360
        else if (max === g) h = 60 * ((b - r) / (max - min)) + 120
        else h = 60 * ((r - g) / (max - min)) + 240
        if (h < 18 || h >= 330) return s > 0.45 ? 'r' : 'y'
        if (h < 68) return 'y'
        if (h < 170) return 'g'
        if (h < 270) return 'b'
        return 'k'
      }
      const rows = []
      for (let y = 0; y < 6; y++) {
        let row = ''
        for (let x = 0; x < 6; x++) {
          const count = { g: 0, y: 0, b: 0, r: 0, k: 0 }
          // The square without its grid lines
          const margin = Math.round(cell * 0.08)
          for (let py = y * cell + margin; py < (y + 1) * cell - margin; py += 2)
            for (let px = x * cell + margin; px < (x + 1) * cell - margin; px += 2) {
              const i = (Math.floor(py) * size + Math.floor(px)) * 4
              count[classOf(data[i], data[i + 1], data[i + 2])]++
            }
          const total = Object.values(count).reduce((a, b) => a + b, 0)
          const [best, n] = Object.entries(count)
            .filter(([c]) => c !== 'k')
            .sort((a, b) => b[1] - a[1])[0]
          row += n / total >= 0.22 ? best : 'k'
        }
        rows.push(row)
      }
      return rows
    },
    `data:image/jpeg;base64,${readFileSync(join(root, tile.image)).toString('base64')}`,
  )
  lines.push(`${tile.name.toLowerCase()} ${rows.join(' ')}`)
}
await browser.close()

const header = [
  '# Couleur dominante de chaque case des tuiles (mesurée sur les images par taleweaver/web/scripts/tile-palette.mjs).',
  '# Une ligne par face : id, puis 6 rangées (y 0→5) de 6 cases (x 0→5), tuile non tournée.',
  '# g vert (mousse, feuillage), y or/ocre/brun (roche, terre, bois), b bleu (eau, glace, cristal), r rouge, k gris ou sombre.',
]
const text = [...header, ...lines].join('\n') + '\n'
if (process.argv.includes('--print')) console.log(text)
else writeFileSync(join(root, 'docs/extraction/tile-palette.txt'), text)
console.log(`${lines.length} faces`)
