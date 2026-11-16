// Finds which tile, and which rotation, a diagram of a booklet shows: so that a scenario's map can be
// read from the diagrams instead of guessed. Works on a page rendered to an image (Middara.Extract render).
//
// Usage (from taleweaver/web):
//   npm run tile-match -- <page.png> --grid <out.png>            the page with a labelled grid every 100 px,
//                                                               to read the rectangle of a tile
//   npm run tile-match -- <page.png> --rect x,y,w,h [--top 5] [--crop <out.png>]
//                                                               the best tiles for that rectangle (one tile,
//                                                               6×6 squares), with their rotation
//   npm run tile-match -- <page.png> --rect x,y,w,h --tiles cols,rows
//                                                               the rectangle is a whole diagram of cols × rows
//                                                               tiles: prints its map.tiles, ready to paste
// The rectangle is in pixels of the image. Figures and tokens drawn over the tile do not spoil the match:
// the score is the median difference of a 12×12 grid of colours, so a few covered cells do not count.
// Tiles of every pack are compared (packs/*/tiles.json).
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const args = process.argv.slice(2)
const option = (name) => {
  const i = args.indexOf(name)
  return i >= 0 ? args[i + 1] : undefined
}
const pagePath = args.find((a, i) => !a.startsWith('--') && !args[i - 1]?.startsWith('--'))
if (!pagePath || (!option('--grid') && !option('--rect'))) {
  console.error(
    'Usage: npm run tile-match -- <page.png> (--grid <out.png> | --rect x,y,w,h [--top 5] [--crop <out.png>])',
  )
  process.exit(1)
}

const dataUrl = (path) => {
  const type = path.toLowerCase().endsWith('.png') ? 'png' : 'jpeg'
  return `data:image/${type};base64,${readFileSync(path).toString('base64')}`
}

const browser = await chromium.launch({ channel: 'msedge' })
const page = await browser.newPage()

if (option('--grid')) {
  const png = await page.evaluate(async (src) => {
    const image = new Image()
    image.src = src
    await image.decode()
    const canvas = document.createElement('canvas')
    canvas.width = image.width
    canvas.height = image.height
    const context = canvas.getContext('2d')
    context.drawImage(image, 0, 0)
    context.font = 'bold 18px sans-serif'
    for (let x = 0; x < image.width; x += 100) {
      context.strokeStyle = x % 500 ? 'rgba(255,0,255,0.5)' : 'rgba(255,0,0,0.9)'
      context.beginPath()
      context.moveTo(x, 0)
      context.lineTo(x, image.height)
      context.stroke()
    }
    for (let y = 0; y < image.height; y += 100) {
      context.strokeStyle = y % 500 ? 'rgba(255,0,255,0.5)' : 'rgba(255,0,0,0.9)'
      context.beginPath()
      context.moveTo(0, y)
      context.lineTo(image.width, y)
      context.stroke()
    }
    context.fillStyle = 'red'
    for (let x = 0; x < image.width; x += 100)
      for (let y = 0; y < image.height; y += 500) context.fillText(`${x}`, x + 3, y + 18)
    for (let y = 100; y < image.height; y += 100)
      for (let x = 0; x < image.width; x += 500) context.fillText(`${y}`, x + 3, y - 4)
    return canvas.toDataURL('image/png').split(',')[1]
  }, dataUrl(pagePath))
  writeFileSync(option('--grid'), Buffer.from(png, 'base64'))
  console.log(`grille écrite : ${option('--grid')}`)
}

if (option('--rect')) {
  const [x, y, w, h] = option('--rect').split(',').map(Number)
  const tiles = readdirSync(join(root, 'packs'))
    .map((pack) => join(root, 'packs', pack, 'tiles.json'))
    .filter(existsSync)
    .flatMap((file) => JSON.parse(readFileSync(file, 'utf8')).tiles)
    .filter((tile) => tile.image && existsSync(join(root, tile.image)))

  // Colours of a 12×12 grid of an image (a rectangle of it), each cell's mean, inner part only
  const [cols, rows] = (option('--tiles') ?? '1,1').split(',').map(Number)
  const rects = []
  for (let row = 0; row < rows; row++)
    for (let col = 0; col < cols; col++) rects.push([x + (col * w) / cols, y + (row * h) / rows, w / cols, h / rows])
  const signatures = await page.evaluate(
    async ({ target, faces, rects }) => {
      const N = 12
      const load = async (src) => {
        const image = new Image()
        image.src = src
        await image.decode()
        return image
      }
      const grid = (image, sx, sy, sw, sh) => {
        const size = N * 10
        const canvas = document.createElement('canvas')
        canvas.width = canvas.height = size
        const context = canvas.getContext('2d')
        context.drawImage(image, sx, sy, sw, sh, 0, 0, size, size)
        const { data } = context.getImageData(0, 0, size, size)
        const cells = []
        for (let cy = 0; cy < N; cy++)
          for (let cx = 0; cx < N; cx++) {
            let r = 0,
              g = 0,
              b = 0,
              n = 0
            for (let py = cy * 10 + 2; py < cy * 10 + 8; py++)
              for (let px = cx * 10 + 2; px < cx * 10 + 8; px++) {
                const i = (py * size + px) * 4
                r += data[i]
                g += data[i + 1]
                b += data[i + 2]
                n++
              }
            cells.push([r / n, g / n, b / n])
          }
        return { cells, crop: canvas.toDataURL('image/png').split(',')[1] }
      }
      const page = await load(target)
      const result = { targets: rects.map((r) => grid(page, r[0], r[1], r[2], r[3])), faces: [] }
      for (const face of faces) {
        const image = await load(face.src)
        result.faces.push({ name: face.name, cells: grid(image, 0, 0, image.width, image.height).cells })
      }
      return result
    },
    {
      target: dataUrl(pagePath),
      faces: tiles.map((t) => ({ name: t.name, src: dataUrl(join(root, t.image)) })),
      rects,
    },
  )
  if (option('--crop')) writeFileSync(option('--crop'), Buffer.from(signatures.targets[0].crop, 'base64'))

  // The tile image turned clockwise by `rotation`, compared cell by cell with the diagram;
  // brightness is evened out (the booklet's print is darker or lighter than the tile scans)
  const N = 12
  const at = (cells, cx, cy, rotation) => {
    const [sx, sy] = {
      0: [cx, cy],
      90: [cy, N - 1 - cx],
      180: [N - 1 - cx, N - 1 - cy],
      270: [N - 1 - cy, cx],
    }[rotation]
    return cells[sy * N + sx]
  }
  const mean = (cells) => cells.reduce((s, c) => s + c[0] + c[1] + c[2], 0) / (cells.length * 3)
  const rank = (target) => {
    const targetMean = mean(target)
    const scores = []
    for (const face of signatures.faces) {
      const scale = targetMean / Math.max(1, mean(face.cells))
      for (const rotation of [0, 90, 180, 270]) {
        const d = []
        for (let cy = 0; cy < N; cy++)
          for (let cx = 0; cx < N; cx++) {
            const a = target[cy * N + cx]
            const b = at(face.cells, cx, cy, rotation)
            d.push(Math.hypot(a[0] - b[0] * scale, a[1] - b[1] * scale, a[2] - b[2] * scale))
          }
        d.sort((p, q) => p - q)
        scores.push({ tile: face.name, rotation, score: d[Math.floor(d.length / 2)] })
      }
    }
    return scores.sort((a, b) => a.score - b.score)
  }

  const doubtful = (scores) => scores[1] && scores[1].score - scores[0].score < 3
  if (rects.length === 1) {
    const scores = rank(signatures.targets[0].cells)
    const top = Number(option('--top') ?? 5)
    console.log('tuile  rotation  écart médian (plus petit = plus proche)')
    for (const s of scores.slice(0, top))
      console.log(`${s.tile.padEnd(6)} ${String(s.rotation).padStart(4)}°   ${s.score.toFixed(1)}`)
    if (doubtful(scores)) console.log("Écart faible avec la suivante : vérifier à l'œil (--crop).")
  } else {
    // A whole diagram: one tile a cell, as map.tiles; an empty cell (no tile) scores badly, say so
    const ref = (name) => tiles.find((t) => t.name === name).id
    const lines = signatures.targets.map((t, i) => {
      const [best, next] = rank(t.cells)
      const col = i % cols
      const row = Math.floor(i / cols)
      const tile = { ref: ref(best.tile), x: col * 6, y: row * 6, rotation: best.rotation }
      const notes = []
      if (best.score > 25) notes.push(`écart ${best.score.toFixed(0)} : case vide ou tuile douteuse`)
      if (doubtful([best, next])) notes.push(`proche de ${next.tile} ${next.rotation}°`)
      return `  ${JSON.stringify(tile)}${i < signatures.targets.length - 1 ? ',' : ''}${notes.length ? '  // ' + notes.join(' ; ') : ''}`
    })
    console.log(['"tiles": [', ...lines, ']'].join('\n'))
  }
}

await browser.close()
