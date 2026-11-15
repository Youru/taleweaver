// Draws the maps of a scenario (or every tile of the catalogue) to PNG files, with the coordinates,
// so that whoever writes a scenario can look at the board: themes that match, and paths between tiles.
// No server needed: the images are read from the repository. Usage (from taleweaver/web):
//   npm run map-preview -- <scenario.json> [output folder]      one PNG per encounter (and per diagram)
//   npm run map-preview -- --tiles [output folder]              contact sheets of the 72 tiles, with their theme
import { mkdirSync, readFileSync, readdirSync, writeFileSync, existsSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { chromium } from '@playwright/test'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const args = process.argv.slice(2)
const S = 60 // pixels per square
const TILE = 6

// The catalogue of every pack
const tiles = {}
const tokens = {}
const figures = {}
for (const pack of readdirSync(join(root, 'packs'))) {
  const folder = join(root, 'packs', pack)
  const read = (name) => (existsSync(join(folder, name)) ? JSON.parse(readFileSync(join(folder, name), 'utf8')) : {})
  for (const t of read('tiles.json').tiles ?? []) tiles[t.id] = t
  for (const t of read('tokens.json').tokens ?? []) tokens[t.id] = t
  for (const c of read('combatants.json').combatants ?? [])
    if (c.miniature && !figures[c.family]) figures[c.family] = { image: c.miniature, baseSize: c.baseSize }
  for (const [name, f] of Object.entries(read('pack.json').figures ?? {})) figures[name] ??= f
}
const url = (path) => (path ? pathToFileURL(join(root, path)).href : '')
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => `&#${c.charCodeAt(0)};`)

/** One board as SVG: tiles (rotated), zones, tokens, figures, and a ruler of coordinates. */
function boardSvg(boards, title) {
  const rects = []
  for (const { map, setup } of boards) {
    for (const t of map.tiles ?? []) rects.push({ x: t.x, y: t.y, w: TILE, h: TILE })
    for (const t of map.tokens ?? []) rects.push({ x: t.x, y: t.y, w: t.w ?? 1, h: t.h ?? 1 })
    for (const c of setup?.combatants ?? []) if (c.at?.x !== undefined) rects.push({ ...c.at, w: 1, h: 1 })
  }
  const width = Math.max(TILE, ...rects.map((r) => r.x + r.w))
  const height = Math.max(TILE, ...rects.map((r) => r.y + r.h))
  const m = 30
  let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width * S + 2 * m}" height="${height * S + 2 * m + 30}" viewBox="${-m} ${-m - 30} ${width * S + 2 * m} ${height * S + 2 * m + 30}">`
  svg += `<rect x="${-m}" y="${-m - 30}" width="${width * S + 2 * m}" height="${height * S + 2 * m + 30}" fill="#111"/>`
  svg += `<text x="0" y="${-m - 8}" fill="#fff" font-size="18" font-family="Segoe UI">${esc(title)}</text>`
  for (let x = 0; x < width; x++)
    svg += `<text x="${x * S + S / 2}" y="-8" fill="#aaa" font-size="13" text-anchor="middle" font-family="Consolas">${x}</text>`
  for (let y = 0; y < height; y++)
    svg += `<text x="-8" y="${y * S + S / 2 + 4}" fill="#aaa" font-size="13" text-anchor="end" font-family="Consolas">${y}</text>`
  boards.forEach(({ map, setup, label }, layer) => {
    for (const t of map.tiles ?? []) {
      const piece = tiles[t.ref]
      const cx = (t.x + TILE / 2) * S
      const cy = (t.y + TILE / 2) * S
      svg += `<image href="${url(piece?.image)}" x="${t.x * S}" y="${t.y * S}" width="${TILE * S}" height="${TILE * S}" preserveAspectRatio="none" transform="rotate(${t.rotation ?? 0} ${cx} ${cy})"/>`
      svg += `<rect x="${t.x * S}" y="${t.y * S}" width="${TILE * S}" height="${TILE * S}" fill="none" stroke="${layer ? '#f59e0b' : '#000'}" stroke-width="3"/>`
      svg += `<text x="${t.x * S + 6}" y="${t.y * S + 20}" fill="#fff" stroke="#000" stroke-width="3" paint-order="stroke" font-size="16" font-weight="700" font-family="Segoe UI">${esc(piece?.name ?? t.ref)} ${t.rotation ?? 0}°${piece?.theme ? ` · ${piece.theme}` : ''}${t.area ? ` · ${t.area}` : ''}${layer ? ` · ${label}` : ''}</text>`
    }
    for (let x = 0; x <= width; x++)
      svg += `<line x1="${x * S}" y1="0" x2="${x * S}" y2="${height * S}" stroke="rgba(255,255,255,0.15)"/>`
    for (let y = 0; y <= height; y++)
      svg += `<line x1="0" y1="${y * S}" x2="${width * S}" y2="${y * S}" stroke="rgba(255,255,255,0.15)"/>`
    for (const z of map.zones ?? []) {
      const r = z.rect
      const color = z.kind === 'startingZone' ? '#38bdf8' : '#f43f5e'
      svg += `<rect x="${r.x * S + 3}" y="${r.y * S + 3}" width="${r.w * S - 6}" height="${r.h * S - 6}" fill="${color}" fill-opacity="0.25" stroke="${color}" stroke-width="4"/>`
      svg += `<text x="${(r.x + r.w / 2) * S}" y="${(r.y + r.h / 2) * S + 5}" fill="#fff" stroke="#000" stroke-width="3" paint-order="stroke" font-size="14" text-anchor="middle" font-family="Segoe UI">${esc(z.id)}</text>`
    }
    for (const t of map.tokens ?? []) {
      const piece = tokens[t.type]
      const w = t.w ?? 1
      const h = t.h ?? 1
      const turned = h > w
      const image = (t.color && piece?.colors?.[t.color]) || piece?.image
      const iw = (turned ? h : w) * S - 6
      const ih = (turned ? w : h) * S - 6
      const cx = (t.x + w / 2) * S
      const cy = (t.y + h / 2) * S
      svg += `<image href="${url(image)}" x="${cx - iw / 2}" y="${cy - ih / 2}" width="${iw}" height="${ih}" preserveAspectRatio="none"${turned ? ` transform="rotate(90 ${cx} ${cy})"` : ''}/>`
      svg += `<rect x="${t.x * S + 1}" y="${t.y * S + 1}" width="${w * S - 2}" height="${h * S - 2}" fill="none" stroke="#fde047" stroke-width="2"/>`
      svg += `<text x="${t.x * S + 3}" y="${(t.y + h) * S - 4}" fill="#fde047" stroke="#000" stroke-width="3" paint-order="stroke" font-size="11" font-family="Consolas">${esc(t.id)}</text>`
    }
    for (const c of setup?.combatants ?? []) {
      if (c.at?.x === undefined) continue
      const family = c.family ?? c.ref?.split('/').pop()
      const n = figures[family]?.baseSize === '2x2' ? 2 : 1
      const cx = (c.at.x + n / 2) * S
      const cy = (c.at.y + n / 2) * S
      svg += `<circle cx="${cx}" cy="${cy}" r="${n * S * 0.44}" fill="#eee" stroke="#dc2626" stroke-width="4"/>`
      if (figures[family])
        svg += `<image href="${url(figures[family].image)}" x="${cx - n * S * 0.4}" y="${cy - n * S * 0.4}" width="${n * S * 0.8}" height="${n * S * 0.8}"/>`
      svg += `<text x="${cx}" y="${cy + n * S * 0.5 + 10}" fill="#fff" stroke="#000" stroke-width="3" paint-order="stroke" font-size="11" text-anchor="middle" font-family="Segoe UI">${esc(family)}</text>`
    }
  })
  return svg + '</svg>'
}

/** Every tile, 6 per row, with its name and theme: to pick tiles that match. */
function tilesSheets() {
  const all = Object.values(tiles).sort((a, b) => a.id.localeCompare(b.id, undefined, { numeric: true }))
  const sheets = []
  for (let i = 0; i < all.length; i += 24) {
    const cells = all
      .slice(i, i + 24)
      .map(
        (t) =>
          `<figure><img src="${url(t.image)}"><figcaption>${esc(t.name)}${t.theme ? ` · ${esc(t.theme)}` : ''}${t.pairedSide ? ` (dos : ${esc(tiles[t.pairedSide]?.name ?? '')})` : ''}</figcaption></figure>`,
      )
      .join('')
    sheets.push(cells)
  }
  return sheets
}

const out = resolve(
  args.find((a, i) => i > 0 && !a.startsWith('--')) ?? (args[0] === '--tiles' ? 'map-preview' : 'map-preview'),
)
mkdirSync(out, { recursive: true })
const pages = []
if (args[0] === '--tiles') {
  tilesSheets().forEach((cells, i) =>
    pages.push({
      name: `tiles-${i + 1}`,
      html: `<style>body{margin:0;background:#111;color:#fff;font:14px Segoe UI}main{display:grid;grid-template-columns:repeat(6,220px);gap:8px;padding:8px}figure{margin:0}img{width:220px;height:220px;display:block}figcaption{padding:2px 0}</style><main>${cells}</main>`,
    }),
  )
} else if (args[0]) {
  const scenario = JSON.parse(readFileSync(resolve(args[0]), 'utf8'))
  for (const node of scenario.nodes.filter((n) => n.type === 'encounter')) {
    const base = { map: node.map ?? {}, setup: node.setup, label: 'map' }
    pages.push({ name: node.id, html: boardSvg([base], `${node.id} : carte de départ`) })
    for (const d of node.diagrams ?? [])
      pages.push({
        name: `${node.id}-${d.id}`,
        html: boardSvg([base, { map: d.map ?? {}, setup: d.setup, label: d.id }], `${node.id} + ${d.id}`),
      })
  }
} else {
  console.error('Usage: npm run map-preview -- <scenario.json> [folder] | --tiles [folder]')
  process.exit(1)
}

const browser = await chromium.launch({ channel: 'msedge' })
try {
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 } })
  for (const { name, html } of pages) {
    // A file page, so that the images of the repository load
    const file = join(out, `${name}.html`)
    writeFileSync(file, `<!doctype html><meta charset="utf-8"><body style="margin:0;background:#111">${html}</body>`)
    await page.goto(pathToFileURL(file).href)
    await page.waitForLoadState('load')
    await page.screenshot({ path: join(out, `${name}.png`), fullPage: true })
    console.log(join(out, `${name}.png`))
  }
} finally {
  await browser.close()
}
