// Makes the PDF of a scenario from the print page, as "Save as PDF" would, without the dialog.
// Needs the app running (API + Vite). Usage:
//   npm run pdf -- <scenario id> [out.pdf] [--node <encounter id>] [--cards] [--clear] [--base http://localhost:5173]
// --clear prints the hidden texts readable (GM's copy) instead of under the red filter.
import { chromium } from '@playwright/test'

const args = process.argv.slice(2)
const option = (name) => {
  const i = args.indexOf(name)
  return i >= 0 ? args.splice(i, 2)[1] : undefined
}
const flag = (name) => {
  const i = args.indexOf(name)
  return i >= 0 && args.splice(i, 1).length > 0
}
const base = option('--base') ?? 'http://localhost:5173'
const node = option('--node')
const cards = flag('--cards')
const clear = flag('--clear')
const [scenario, out = `${scenario?.split('/').pop() ?? 'scenario'}${cards ? '-cards' : node ? `-${node}` : ''}.pdf`] =
  args
if (!scenario) {
  console.error('Usage: npm run pdf -- <scenario id> [out.pdf] [--node <id>] [--cards] [--clear] [--base <url>]')
  process.exit(1)
}

const query = new URLSearchParams({ scenario })
if (node) query.set('node', node)
if (cards) query.set('what', 'cards')
if (clear) query.set('veiled', '0')

const browser = await chromium.launch({ channel: 'msedge' })
try {
  const page = await browser.newPage()
  await page.goto(`${base}/workshop/print?${query}`)
  // Pages drawn and every image loaded
  await page.locator('.sheets').waitFor()
  // The booklet's page numbers, once every step has measured its sheets
  if (!node && !cards) await page.locator('.sheets[data-paged]').waitFor()
  await page.waitForLoadState('networkidle')
  await page.emulateMedia({ media: 'print' })
  await page.pdf({ path: out, preferCSSPageSize: true, printBackground: true })
  console.log(`PDF written: ${out}`)
} finally {
  await browser.close()
}
