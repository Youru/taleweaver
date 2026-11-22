import { networkInterfaces } from 'node:os'
import { expect, test, type Browser, type Page } from '@playwright/test'

// The screens linked by QR code (room-spec §7.6), by the PC's Wi-Fi address: on localhost any page is the GM's.
const wifi = Object.values(networkInterfaces())
  .flat()
  .some((a) => a?.family === 'IPv4' && !a.internal && /^(10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.)/.test(a.address))
test.skip(!wifi, 'no private address to come from')

async function scan(pc: Page, browser: Browser, code: string, viewport: { width: number; height: number }) {
  await pc.getByRole('button', { name: 'Choisir les écrans' }).click()
  await pc.getByRole('button', { name: code, exact: true }).click()
  const url = (await pc.locator('code.link').textContent())!
  expect(url).not.toContain('localhost')
  const context = await browser.newContext({ viewport, hasTouch: true, isMobile: true, locale: 'fr-FR' })
  const device = await context.newPage()
  await device.goto(url)
  // Scanned: the PC hides the code
  await expect(pc.locator('code.link')).toHaveCount(0)
  await pc.keyboard.press('Escape')
  await expect(pc.getByRole('dialog')).toHaveCount(0)
  return { context, device, url }
}

async function prepare(pc: Page) {
  await pc.goto('/')
  await pc.getByRole('button', { name: 'Jouer Blight of Rhamsted' }).click()
  await pc.getByRole('button', { name: 'Préparer le groupe' }).click()
  await expect(pc).toHaveURL(/\/games\/\w+/)
  return pc.url().split('/').pop()!
}

/** What stops a finger: the page wider than the screen, a button out of it or too small. */
async function fitsAFinger(device: Page) {
  await expect
    .poll(() =>
      device.evaluate(() => {
        const problems: string[] = []
        if (document.documentElement.scrollWidth > innerWidth + 1)
          problems.push(`page ${document.documentElement.scrollWidth} > ${innerWidth}`)
        for (const el of document.querySelectorAll<HTMLElement>('button, [role=tab], [role=combobox], a.p-button')) {
          const box = el.getBoundingClientRect()
          if (!box.width || !box.height || getComputedStyle(el).visibility === 'hidden') continue
          const name = (el.getAttribute('aria-label') || el.textContent || el.className).trim().slice(0, 30)
          if (Math.min(box.width, box.height) < 39.5)
            problems.push(`small ${name}: ${Math.round(box.width)}x${Math.round(box.height)}`)
          if (box.left < -1 && !el.closest('.p-tablist, .track')) problems.push(`left ${name}`)
        }
        return [...new Set(problems)]
      }),
    )
    .toEqual([])
}

for (const viewport of [
  { width: 1280, height: 800 },
  { width: 800, height: 1280 },
]) {
  test(`the GM plays on a ${viewport.width}x${viewport.height} tablet, from the group to the end`, async ({
    page,
    browser,
  }, info) => {
    test.setTimeout(120_000)
    const id = await prepare(page)
    const { context, device } = await scan(page, browser, 'QR code du MJ', viewport)
    const shot = (name: string) => device.screenshot({ path: info.outputPath(`${name}.png`) })
    await expect(device).toHaveURL(new RegExp(`/games/${id}$`))

    await device.getByRole('button', { name: 'Ajouter Nightingale' }).tap()
    await device.getByRole('button', { name: 'Ajouter Rook' }).tap()
    await shot('group')
    await expect.soft(fitsAFinger(device)).resolves.toBeUndefined()
    await device.getByRole('button', { name: 'Partir à l’aventure' }).tap()
    await device.getByRole('button', { name: 'Continuer' }).tap()
    await device.getByRole('button', { name: 'Commencer le round 1' }).tap()

    const yes = device.getByRole('button', { name: 'Oui', exact: true })
    const next = device.getByRole('button', { name: 'Tour suivant' })
    for (let turn = 0; turn < 8; turn++) {
      await expect(async () => expect((await yes.isVisible()) || (await next.isEnabled())).toBe(true)).toPass()
      if (await yes.isVisible()) break
      await next.tap()
    }
    await shot('turn')
    // Without scrolling
    await expect.soft(yes).toBeInViewport({ ratio: 1 })
    await expect.soft(fitsAFinger(device)).resolves.toBeUndefined()
    await yes.tap()
    await device.getByRole('tab', { name: 'Journal' }).tap()
    await expect(device.getByText('Réponse : Oui').first()).toBeVisible()
    // The PC follows
    await page.getByRole('tab', { name: 'Journal' }).click()
    await expect(page.getByText('Réponse : Oui').first()).toBeVisible()

    for (const name of ['Inventaire', 'Room', 'Menu']) {
      await device
        .locator('.game-bar button')
        .and(device.getByRole('button', { name: new RegExp(name) }))
        .first()
        .tap()
      await device.waitForTimeout(400)
      await shot(name)
      await expect.soft(fitsAFinger(device)).resolves.toBeUndefined()
      await device.keyboard.press('Escape')
    }
    await expect(device.getByRole('button', { name: 'Choisir les écrans' })).toHaveCount(0)

    // The network drops: the page stays, then catches up
    await context.route('**/api/games/**', (route) => route.abort())
    await expect(device.getByText(/Connexion au PC du MJ perdue/)).toBeVisible({ timeout: 25_000 })
    await expect(device.getByRole('tab', { name: 'Journal' })).toBeVisible()
    await context.unroute('**/api/games/**')
    await expect(device.getByText(/Connexion au PC du MJ perdue/)).toBeHidden({ timeout: 20_000 })

    await device.goto(new URL('/', device.url()).href)
    await expect(device).toHaveURL(new RegExp(`/games/${id}$`))

    // The duel, by finger
    const board = device.getByRole('img', { name: 'Plateau : Search for the Lair' })
    await board.getByRole('button', { name: 'Figurine Rook' }).tap()
    await device.getByRole('button', { name: 'Attaquer', exact: true }).tap()
    const duel = device.getByRole('dialog')
    await expect(duel).toBeVisible()
    await shot('duel')
    await expect.soft(fitsAFinger(device)).resolves.toBeUndefined()
    await device.keyboard.press('Escape')
    await expect(duel).toBeHidden()

    // To the end of the scenario: the GM declares what the table reached, and answers what is asked
    const ending = device.getByText('Recommencer et exporter se font sur le PC du MJ.')
    const choice = device.locator('.story .prompt button:enabled, .turn .prompt button:enabled').first()
    const reached = device.locator('.goals .end button:enabled').first()
    const goals = device.getByRole('tab', { name: 'Rencontre' })
    for (let step = 0; step < 40 && !(await ending.isVisible()); step++) {
      if (await choice.isVisible()) await choice.tap()
      else if (await reached.isVisible()) await reached.tap()
      else if (await goals.isVisible()) await goals.tap()
      await device.waitForTimeout(500)
    }
    await shot('end')
    await expect(ending).toBeVisible()
    await context.close()
  })
}

test('the table shows the board, never the game, until the PC disconnects it', async ({ page, browser }, info) => {
  const id = await prepare(page)
  await page.getByRole('button', { name: 'Ajouter Nightingale' }).click()
  await page.getByRole('button', { name: 'Ajouter Rook' }).click()
  await page.getByRole('button', { name: 'Partir à l’aventure' }).click()
  const { context, device, url } = await scan(page, browser, 'QR code de la table', { width: 1280, height: 800 })
  const read: string[] = []
  device.on('request', (request) => read.push(new URL(request.url()).pathname))
  await expect(device).toHaveURL(/\/table$/)

  await page.getByRole('button', { name: 'Continuer' }).click()
  await page.getByRole('button', { name: 'Commencer le round 1' }).click()
  await expect(device.getByText(/Round 1/)).toBeVisible()
  // The cards in play, served through the phones' white list
  await expect(device.locator('.initiative img').first()).toBeVisible()
  await device.screenshot({ path: info.outputPath('table.png') })

  await device.goto(new URL(`/games/${id}`, device.url()).href)
  await expect(device).toHaveURL(/\/table$/)
  expect(read.filter((path) => /^\/api\/(games|scenarios|packs)/.test(path))).toEqual([])

  const second = await browser.newContext()
  const late = await second.newPage()
  await late.goto(url)
  await expect(late.getByRole('alert')).toContainText('déjà servi')
  await second.close()

  await page.getByRole('button', { name: 'Choisir les écrans' }).click()
  await page.getByRole('button', { name: 'Déconnecter les tables' }).click()
  await expect(device.getByText(/n’est plus relié/)).toBeVisible()
  await context.close()
})
