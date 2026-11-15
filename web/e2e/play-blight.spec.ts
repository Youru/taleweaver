import { expect, test, type Page } from '@playwright/test'

async function startBlight(page: Page) {
  await page.goto('/')
  await page.getByRole('button', { name: 'Jouer' }).first().click()
  await page.getByLabel('Aventurier 1').fill('Nightingale')
  await page.getByLabel('Aventurier 2').fill('Rook')
  await page.getByRole('button', { name: 'Commencer' }).click()
  await expect(page).toHaveURL(/\/games\/\w+/)
}

test('a GM starts Blight of Rhamsted, reads the intro and plays the first AI turn', async ({ page }) => {
  await startBlight(page)
  await expect(page.getByRole('heading', { name: 'Blight of Rhamsted' })).toBeVisible()

  await page.getByRole('button', { name: 'Continuer' }).click()
  await expect(page.getByRole('heading', { name: 'Search for the Lair' })).toBeVisible()
  await expect(page.getByText('fragor-3', { exact: true })).toBeVisible()

  await page.getByRole('button', { name: 'Commencer le round 1' }).click()
  await expect(
    page.getByRole('region', { name: 'Search for the Lair' }).getByText('Round 1', { exact: true }),
  ).toBeVisible()
  // Next turns until a combatant's AI asks its first condition
  const yes = page.getByRole('button', { name: 'Oui' })
  const next = page.getByRole('button', { name: 'Tour suivant' })
  for (let turn = 0; turn < 8; turn++) {
    // Wait for the last command's answer: either a question, or the next turn can be played
    await expect(async () => expect((await yes.isVisible()) || (await next.isEnabled())).toBe(true)).toPass()
    if (await yes.isVisible()) break
    await next.click()
  }
  await expect(page.getByText(/^IA de /)).toBeVisible()
  await yes.click()
  await expect(page.getByText('Réponse : Oui')).toBeVisible()
})

test('a GM deals damage, then undoes it', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()

  await page.getByLabel('Dégâts fragor-1').fill('5')
  await page.getByRole('button', { name: 'Infliger' }).first().click()
  await expect(page.getByText('5 / 12 PV')).toBeVisible()

  await page.getByRole('button', { name: 'Annuler la dernière action' }).click()
  await expect(page.getByText('0 / 12 PV').first()).toBeVisible()
  await expect(page.getByText('5 / 12 PV')).toHaveCount(0)
})

test('a GM sees the setup on the table and activates a totem from it', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()

  const board = page.getByRole('img', { name: 'Plateau : Search for the Lair' })
  await expect(board).toBeVisible()
  // The 5 tiles are drawn from the local images
  const tiles = board.locator('g.tile image')
  await expect(tiles).toHaveCount(5)
  expect(await tiles.first().evaluate((img) => img.getAttribute('href'))).toBe('/api/assets/act1/tiles/um47.jpg')
  const loaded = await page.request.get('/api/assets/act1/tiles/um47.jpg')
  expect(loaded.headers()['content-type']).toBe('image/jpeg')
  // 6 combatants, and the 2 adventurers in the starting zone
  await expect(board.locator('g.figure.combatant')).toHaveCount(6)
  await expect(board.locator('g.figure.adventurer')).toHaveCount(2)

  await board.getByRole('button', { name: 'Jeton totem:blue' }).click()
  await page.getByRole('button', { name: 'Activer' }).click()
  // The Earth Loa spawns on the totem; the 2 Gevaudan are for the GM to place
  await expect(page.getByText('earth-loa-2', { exact: true })).toBeVisible()
  await expect(board.locator('g.figure.combatant')).toHaveCount(7)
  await expect(page.getByRole('button', { name: 'gevaudan-1' })).toBeVisible()
  await expect(board.getByRole('button', { name: 'Jeton totem:blue' })).toHaveCount(0)
})

test('a GM moves figures on the table, by dragging or by tapping a square', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()
  const board = page.getByRole('img', { name: 'Plateau : Search for the Lair' })
  const fragor = board.getByRole('button', { name: 'Figurine fragor-1' })
  await expect(fragor).toBeVisible()
  const before = await fragor.boundingBox()

  // Drag one square to the west (squares are a sixth of a tile)
  const tile = (await board.locator('g.tile image').first().boundingBox())!
  const square = tile.width / 6
  await page.mouse.move(before!.x + before!.width / 2, before!.y + before!.height / 2)
  await page.mouse.down()
  await page.mouse.move(before!.x + before!.width / 2 - square / 2, before!.y + before!.height / 2, { steps: 4 })
  await page.mouse.move(before!.x + before!.width / 2 - square, before!.y + before!.height / 2, { steps: 4 })
  await page.mouse.up()
  await expect.poll(async () => (await fragor.boundingBox())!.x).toBeLessThan(before!.x - square / 2)

  // Select Rook, then tap a square: Rook goes there
  const rook = board.getByRole('button', { name: 'Figurine Rook' })
  await rook.click()
  await expect(page.getByRole('region', { name: 'Sélection : Rook' })).toBeVisible()
  await page.mouse.click(tile.x + square * 2.5, tile.y + square * 2.5)
  await expect
    .poll(async () => {
      const box = (await rook.boundingBox())!
      return [
        Math.floor((box.x + box.width / 2 - tile.x) / square),
        Math.floor((box.y + box.height / 2 - tile.y) / square),
      ]
    })
    .toEqual([2, 2])

  // Moves are commands: undo puts Rook back
  await page.getByRole('button', { name: 'Annuler la dernière action' }).click()
  await expect.poll(async () => (await rook.boundingBox())!.x).toBeGreaterThan(tile.x + square * 6)
})

test('the table alone follows the game', async ({ page, context }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()
  const [table] = await Promise.all([
    context.waitForEvent('page'),
    page.getByRole('link', { name: 'Table seule' }).click(),
  ])

  await expect(table.getByRole('img', { name: 'Plateau : Search for the Lair' })).toBeVisible()
  await page.getByRole('button', { name: 'Commencer le round 1' }).click()
  await expect(table.getByText('Round 1', { exact: true })).toBeVisible()
})

test('the home page lists the saved game, which can be deleted', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('link', { name: 'Taleweaver' }).click()

  const saved = page.getByRole('listitem').filter({ hasText: 'Nightingale, Rook' }).first()
  await expect(saved).toBeVisible()
  const before = await page.getByRole('button', { name: 'Reprendre' }).count()
  await saved.getByRole('button', { name: 'Supprimer' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: 'Supprimer' }).click()
  await expect(page.getByRole('button', { name: 'Reprendre' })).toHaveCount(before - 1)
})
