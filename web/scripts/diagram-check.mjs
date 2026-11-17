// Lays a scenario's maps over the diagrams of its booklet, to see at a glance a token or a figure put on
// the wrong square: the page is rendered (Middara.Extract render), the board found on it (its dark
// rectangle), then every piece of our map is outlined on top: tiles in white, terrain in orange, other
// tokens in cyan, figures in magenta, each with its id. Usage (from taleweaver/web):
//   npm run diagram-check -- <scenario.json> <booklet.pdf> <output folder> [--node <encounter id>]
//        [--diagram <diagram id> --page <n>] [--board x,y,square] [--dpi 200]
// An encounter is drawn on its `page`; a diagram (laid over its encounter's board) on the page given by
// --page, its pieces in bold. The board is found as the dark rectangle below the header; when an
// illustration spoils it, give it: --board x,y,square (in pixels at the dpi used, top-left of square 0,0).
// The output is cropped to the board. Needs tools/Middara.Extract built in Release.
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const args = process.argv.slice(2)
const option = (name) => {
  const i = args.indexOf(name)
  return i >= 0 ? args.splice(i, 2)[1] : undefined
}
const only = option('--node')
const diagramId = option('--diagram')
const diagramPage = option('--page')
const boardOption = option('--board')?.split(',').map(Number)
const DPI = Number(option('--dpi') ?? 200)
const [scenarioPath, pdf, out] = args
if (!scenarioPath || !pdf || !out) {
  console.error('Usage: npm run diagram-check -- <scenario.json> <booklet.pdf> <output folder> [--node <id>]')
  process.exit(1)
}
mkdirSync(out, { recursive: true })
const extract = join(root, 'tools/Middara.Extract/bin/Release/net10.0/Middara.Extract.exe')

// Sizes of the token types, to draw a token without w/h
const sizes = {}
for (const pack of readdirSync(join(root, 'packs'))) {
  const file = join(root, 'packs', pack, 'tokens.json')
  if (!existsSync(file)) continue
  for (const t of JSON.parse(readFileSync(file, 'utf8')).tokens ?? [])
    sizes[t.id] = { size: t.size, category: t.category }
}
const sizeOf = (t) => ({ w: t.w ?? 1, h: t.h ?? 1 })
const big = (family) => /defender|queen|docious|loa|gevaudan/.test(family ?? '')

function pieces(map, setup, bold) {
  const list = []
  for (const tile of map?.tiles ?? [])
    list.push({ kind: 'tile', x: tile.x, y: tile.y, w: 6, h: 6, id: tile.ref.split('/').pop(), bold })
  for (const t of map?.tokens ?? []) {
    const terrain = sizes[t.type]?.category === 'terrain'
    list.push({ kind: terrain ? 'terrain' : 'token', x: t.x, y: t.y, ...sizeOf(t), id: t.id, bold })
  }
  for (const c of setup?.combatants ?? [])
    if (c.at) {
      const n = big(c.family) ? 2 : 1
      list.push({ kind: 'figure', x: c.at.x, y: c.at.y, w: n, h: n, id: c.family ?? c.ref ?? '?', bold })
    }
  return list
}

const scenario = JSON.parse(readFileSync(scenarioPath, 'utf8'))
const jobs = []
for (const node of scenario.nodes.filter((n) => n.type === 'encounter' && (!only || n.id === only))) {
  const base = pieces(node.map, node.setup, false)
  if (diagramId) {
    const diagram = (node.diagrams ?? []).find((d) => d.id === diagramId)
    if (!diagram) continue
    jobs.push({
      name: `${node.id}-${diagram.id}`,
      page: Number(diagramPage),
      list: [...base, ...pieces(diagram.map, diagram.setup, true)],
    })
  } else if (node.page || diagramPage) {
    // --page: another page of the same board (a large map printed over two pages)
    const at = Number(diagramPage ?? node.page)
    jobs.push({ name: diagramPage ? `${node.id}-p${at}` : node.id, page: at, list: base })
  }
}

const browser = await chromium.launch({ channel: 'msedge' })
const page = await browser.newPage()
for (const job of jobs) {
  const dir = join(out, 'pages')
  execFileSync(extract, ['render', pdf, dir, '--pages', String(job.page), '--dpi', String(DPI), '--force'], {
    stdio: 'ignore',
  })
  const png = readFileSync(join(dir, `page-${String(job.page).padStart(3, '0')}.png`)).toString('base64')
  const width = Math.max(...job.list.map((p) => p.x + p.w))
  const height = Math.max(...job.list.map((p) => p.y + p.h))
  const data = await page.evaluate(
    async ({ png, list, width, height, given }) => {
      const img = new Image()
      img.src = `data:image/png;base64,${png}`
      await img.decode()
      const c = document.createElement('canvas')
      c.width = img.width
      c.height = img.height
      const g = c.getContext('2d')
      g.drawImage(img, 0, 0)
      // The board: the rows and columns, below the header, mostly dark
      const px = g.getImageData(0, 0, c.width, c.height).data
      const dark = (x, y) => {
        const i = (y * c.width + x) * 4
        return px[i] * 0.3 + px[i + 1] * 0.59 + px[i + 2] * 0.11 < 70
      }
      const top = Math.round(c.height * 0.22)
      const rows = []
      for (let y = top; y < c.height; y++) {
        let n = 0
        for (let x = 0; x < c.width; x += 2) n += dark(x, y) ? 1 : 0
        if (n > c.width / 2 / 4) rows.push(y)
      }
      const y0 = rows[0]
      const y1 = rows[rows.length - 1]
      const cols = []
      for (let x = 0; x < c.width; x++) {
        let n = 0
        for (let y = y0; y <= y1; y += 2) n += dark(x, y) ? 1 : 0
        if (n > (y1 - y0) / 2 / 3) cols.push(x)
      }
      const x0 = cols[0]
      const x1 = cols[cols.length - 1]
      const sq = given ? given[2] : Math.min((x1 - x0) / width, (y1 - y0) / height)
      const bx = given ? given[0] : x0
      const by = given ? given[1] : y0
      const colors = { tile: '#ffffff', terrain: '#ff9900', token: '#00e5ff', figure: '#ff00d4' }
      g.font = `${Math.round(sq / 4)}px sans-serif`
      for (const p of list) {
        g.strokeStyle = colors[p.kind]
        g.lineWidth = p.kind === 'tile' ? 1 : p.bold ? 4 : 2
        g.setLineDash(p.kind === 'terrain' ? [5, 3] : [])
        const inset = p.kind === 'tile' ? 0 : p.kind === 'terrain' ? 3 : 7
        const rx = bx + p.x * sq + inset
        const ry = by + p.y * sq + inset
        if (p.kind === 'figure' || p.kind === 'token') {
          g.beginPath()
          g.arc(rx - inset + (p.w * sq) / 2, ry - inset + (p.h * sq) / 2, (p.w * sq) / 2 - inset, 0, 2 * Math.PI)
          g.stroke()
        } else g.strokeRect(rx, ry, p.w * sq - 2 * inset, p.h * sq - 2 * inset)
        g.fillStyle = colors[p.kind]
        if (p.kind !== 'tile') g.fillText(p.id.replace(/^cave-sickle-?/, 'cs-').slice(0, 12), rx + 2, ry + sq / 3)
      }
      // Only the board (and a margin)
      const m = sq / 2
      const crop = document.createElement('canvas')
      crop.width = width * sq + 2 * m
      crop.height = height * sq + 2 * m
      crop.getContext('2d').drawImage(c, bx - m, by - m, crop.width, crop.height, 0, 0, crop.width, crop.height)
      return { url: crop.toDataURL('image/png').split(',')[1], board: [bx, by], sq }
    },
    { png, list: job.list, width, height, given: boardOption },
  )
  writeFileSync(join(out, `${job.name}.png`), Buffer.from(data.url, 'base64'))
  console.log(`${job.name}: page ${job.page}, board ${data.board.join(',')}, ${data.sq.toFixed(1)} px/square`)
}
await browser.close()
