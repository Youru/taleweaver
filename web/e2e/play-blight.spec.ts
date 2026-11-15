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
  // The current turn card shows whose AI it is, with its question
  const turn = page.getByRole('region', { name: 'Tour en cours' })
  await expect(turn.getByText(/^IA de /).first()).toBeVisible()
  await yes.click()
  await page.getByRole('tab', { name: 'Journal' }).click()
  await expect(page.getByText('Réponse : Oui')).toBeVisible()
})

test('the AI helper answers from the table, then shows the target or the move', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()
  await page.getByRole('button', { name: 'Commencer le round 1' }).click()
  const yes = page.getByRole('button', { name: 'Oui' })
  const next = page.getByRole('button', { name: 'Tour suivant' })
  for (let turn = 0; turn < 8; turn++) {
    await expect(async () => expect((await yes.isVisible()) || (await next.isEnabled())).toBe(true)).toPass()
    if (await yes.isVisible()) break
    await next.click()
  }

  // Questions read from the table come with the board's answer: follow it, turn after turn, until a
  // suggested answer led to a move (a condition the board cannot tell gets "Non").
  const turn = page.getByRole('region', { name: 'Tour en cours' })
  const advice = turn.getByRole('list', { name: 'Suggestions du plateau' })
  const suggestion = turn.getByText(/Suggestion : (Oui|Non) — /)
  const move = advice.getByRole('button', { name: /^Déplacer / })
  let suggested = false
  /** Clicks, then waits for the game the API sends back (the card is redrawn from it). */
  const play = (button: ReturnType<Page['getByRole']>) =>
    Promise.all([
      page.waitForResponse((r) => r.request().method() === 'POST' && r.url().includes('/api/games/')),
      button.click(),
    ])
  for (let step = 0; step < 40; step++) {
    await expect(async () => expect((await yes.isVisible()) || (await next.isEnabled())).toBe(true)).toPass()
    if (await yes.isVisible()) {
      const hint = (await suggestion.isVisible()) ? await suggestion.textContent() : null
      suggested ||= !!hint
      const answer = /Suggestion : (Oui|Non)/.exec(hint ?? '')?.[1] ?? 'Non'
      await play(page.getByRole('button', { name: answer, exact: true }))
      continue
    }
    if (suggested && (await move.first().isVisible())) break
    await play(next)
  }
  expect(suggested).toBe(true)
  await expect(move.first()).toBeVisible()
  await expect(turn.getByText('Calculé sans murs ni ligne de vue : vérifiez sur la table.')).toBeVisible()

  // A suggested move is drawn on the table and played in one click
  await expect(page.locator('g.hint rect').first()).toBeVisible()
  await move.first().click()
  await expect(advice.getByText('Déplacé').first()).toBeVisible()
})

test('a GM deals damage, then undoes it', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()

  // A combatant picked in the list opens its actions on the table
  await page
    .getByRole('button', { name: /fragor-1/ })
    .first()
    .click()
  await page.getByLabel('Dégâts fragor-1').fill('5')
  await page.getByRole('button', { name: 'Infliger' }).click()
  await expect(page.getByText('5 / 12 PV').first()).toBeVisible()

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
  await expect(page.getByRole('button', { name: 'gevaudan-1', exact: true })).toBeVisible()
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

test('a GM drags a figure from "to place" onto the board', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()
  const board = page.getByRole('img', { name: 'Plateau : Search for the Lair' })
  await board.getByRole('button', { name: 'Jeton totem:blue' }).click()
  await page.getByRole('button', { name: 'Activer' }).click()
  const toPlace = page.getByRole('button', { name: 'gevaudan-1', exact: true })
  await expect(toPlace).toBeVisible()

  const from = (await toPlace.boundingBox())!
  const tile = (await board.locator('g.tile image').nth(2).boundingBox())!
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
  await page.mouse.down()
  await page.mouse.move(tile.x + tile.width / 2, tile.y + tile.height / 2, { steps: 10 })
  await page.mouse.up()

  await expect(board.getByRole('button', { name: 'Figurine gevaudan-1' })).toBeVisible()
  await expect(toPlace).toHaveCount(0)
})

test('a GM resolves an attack in the duel window by tapping the dice faces, then a spell', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()
  const board = page.getByRole('img', { name: 'Plateau : Search for the Lair' })

  // Rook attacks from the table: unarmed (2 purple dice) against the nearest fragor (Defense 11)
  await board.getByRole('button', { name: 'Figurine Rook' }).click()
  await page.getByRole('button', { name: 'Attaquer', exact: true }).click()
  const duel = page.getByRole('dialog')
  await expect(duel.getByRole('combobox', { name: 'Attaquant' })).toHaveText('Rook')
  await expect(duel.getByRole('combobox', { name: 'Cible' })).toHaveText(/^fragor-2 · Fragor/)
  await expect(duel.getByRole('status', { name: 'Chances' })).toContainText('22')

  // The odds can be hidden for tables that prefer to roll blind
  await duel.getByRole('switch').click()
  await expect(duel.getByRole('status', { name: 'Chances' })).toHaveCount(0)

  // The table rolled a 7 on each die: 14 against 11, hit by 3; Rook has no symbol ability
  await duel.getByRole('radiogroup', { name: 'Dé 1 · Violet' }).getByRole('radio', { name: /^7,/ }).click()
  await duel.getByRole('radiogroup', { name: 'Dé 2 · Violet' }).getByRole('radio', { name: /^7,/ }).click()
  await expect(duel.getByText('Jet 14 contre Défense 11 : touché, écart de 3.')).toBeVisible()
  await duel.getByRole('button', { name: 'Infliger 3 dégâts à fragor-2' }).click()
  await expect(duel.getByText('Dégâts infligés')).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(duel).toBeHidden()
  await expect(page.getByText('3 / 12 PV').first()).toBeVisible()

  // A Spell-Like Ability of the fragor (Force 6) against Nightingale's Conviction: 2 + 3 = 5, she fails
  await board.getByRole('button', { name: 'Figurine fragor-1' }).click()
  await page.getByRole('button', { name: 'Attaquer', exact: true }).click()
  await duel.getByRole('button', { name: 'Sort' }).click()
  await duel.getByRole('radiogroup', { name: /dé 1/ }).getByRole('radio', { name: '2', exact: true }).click()
  await duel.getByRole('radiogroup', { name: /dé 2/ }).getByRole('radio', { name: /^3,/ }).click()
  await expect(duel.getByText('Le sort agit')).toBeVisible()
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

test('a GM puts the table in full screen, then leaves it', async ({ page }) => {
  await startBlight(page)
  await page.getByRole('button', { name: 'Continuer' }).click()
  await expect(page.getByRole('img', { name: 'Plateau : Search for the Lair' })).toBeVisible()

  await page.getByRole('button', { name: 'Plein écran' }).click()
  await expect.poll(() => page.evaluate(() => document.fullscreenElement?.className ?? '')).toContain('table')
  await expect(page.getByRole('img', { name: 'Plateau : Search for the Lair' })).toBeVisible()

  await page.getByRole('button', { name: 'Quitter le plein écran' }).click()
  await expect.poll(() => page.evaluate(() => document.fullscreenElement)).toBeNull()
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
