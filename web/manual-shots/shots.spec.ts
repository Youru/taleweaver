import { mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { test, type Locator, type Page } from '@playwright/test'

/**
 * The pictures of the manual (public/help/<language>/*.png), with numbered marks that the manual's legends explain
 * (features/manual/content). Taken on the demonstration content (playwright.manual.config.ts): nothing of the game.
 *
 *     npm run manual:shots
 *
 * The numbers here and the legends there go together: change one, change the other.
 */
const languages = ['fr', 'en'] as const
type Language = (typeof languages)[number]

async function mark(page: Page, marks: [Locator, number][]) {
  const boxes = []
  for (const [locator, n] of marks) {
    const box = await locator.first().boundingBox()
    if (!box) throw new Error(`Mark ${n}: element not on screen`)
    boxes.push({ ...box, n })
  }
  await page.evaluate((all) => {
    for (const b of all) {
      const frame = document.createElement('div')
      frame.className = 'manual-mark'
      Object.assign(frame.style, {
        position: 'fixed',
        left: `${b.x - 4}px`,
        top: `${b.y - 4}px`,
        width: `${b.width + 8}px`,
        height: `${b.height + 8}px`,
        border: '3px solid #e8a317',
        borderRadius: '8px',
        boxShadow: '0 0 0 2px rgb(0 0 0 / 0.35)',
        zIndex: '99998',
        pointerEvents: 'none',
      })
      const badge = document.createElement('div')
      badge.className = 'manual-mark'
      badge.textContent = String(b.n)
      Object.assign(badge.style, {
        position: 'fixed',
        left: `${Math.max(2, b.x - 16)}px`,
        top: `${Math.max(2, b.y - 16)}px`,
        width: '26px',
        height: '26px',
        borderRadius: '50%',
        background: '#e8a317',
        color: '#1a1a1a',
        font: '700 15px/26px system-ui, sans-serif',
        textAlign: 'center',
        boxShadow: '0 1px 4px rgb(0 0 0 / 0.5)',
        zIndex: '99999',
        pointerEvents: 'none',
      })
      document.body.append(frame, badge)
    }
  }, boxes)
}

async function shot(page: Page, language: Language, name: string, marks: [Locator, number][] = [], height?: number) {
  await page.waitForTimeout(600)
  await mark(page, marks)
  const folder = fileURLToPath(new URL(`../public/help/${language}/`, import.meta.url))
  mkdirSync(folder, { recursive: true })
  await page.screenshot({ path: `${folder}${name}.png`, clip: height ? { x: 0, y: 0, width: 1280, height } : undefined })
  await page.evaluate(() => document.querySelectorAll('.manual-mark').forEach((m) => m.remove()))
}

for (const language of languages) {
  test(`manual pictures (${language})`, async ({ page }) => {
    test.setTimeout(180_000)
    const fr = language === 'fr'
    await page.addInitScript((value) => localStorage.setItem('taleweaver.language', value), language)
    const button = (frName: string, enName: string) => page.getByRole('button', { name: fr ? frName : enName, exact: true })

    // Home
    await page.goto('/')
    const scenario = page.getByRole('button', { name: fr ? /^Jouer La clairière/ : /^Play The bandit clearing/ })
    await shot(page, language, 'home', [
      [scenario, 1],
      [page.getByRole('heading', { name: fr ? 'Parties sauvegardées' : 'Saved games' }), 2],
      [page.getByRole('link', { name: fr ? 'Atelier' : 'Workshop' }), 3],
      [page.getByRole('link', { name: fr ? 'Règles' : 'Rules' }), 4],
      [button('Aide', 'Help'), 5],
    ])

    // Party
    await scenario.click()
    await button('Préparer le groupe', 'Prepare the party').click()
    await page.getByRole('button', { name: 'Mira' }).click()
    await page.getByRole('button', { name: 'Tobin' }).click()
    await shot(page, language, 'party', [
      [page.locator('main').getByText('Mira', { exact: true }).last().locator('../..'), 1],
      [page.getByRole('textbox', { name: fr ? /aventurier/ : /adventurer/ }), 2],
      [page.getByRole('button', { name: fr ? /Faire choisir les joueurs/ : /Let the players choose/ }), 3],
      [page.getByRole('button', { name: fr ? /Partir à l’aventure/ : /Set out/ }), 4],
      [page.getByRole('button', { name: /^M|Mira/ }).first(), 5],
    ])

    // Story
    await page.getByRole('button', { name: fr ? /Partir à l’aventure/ : /Set out/ }).click()
    await shot(page, language, 'story', [
      [page.getByText(fr ? /Une fumée monte/ : /Smoke rises/), 1],
      [button('Continuer', 'Continue'), 2],
    ])

    // Setup of the encounter
    await button('Continuer', 'Continue').click()
    const board = page.locator('svg[role=img]').first()
    await board.waitFor()
    await shot(page, language, 'setup', [
      [board, 1],
      [page.getByRole('button', { name: fr ? /Prêts/ : /Ready/ }), 2],
      [page.getByRole('button', { name: fr ? /Table seule/ : /Table only/ }), 3],
      [page.getByRole('tab', { name: fr ? 'Combattants' : 'Combatants' }), 4],
      [page.getByRole('button', { name: fr ? /Commencer le round 1/ : /Start round 1/ }), 5],
    ])
    // The bar of a game: undo, inventory, room, screens, sheet, rules, menu
    const bar = page.locator('#app-bar button:visible')
    await shot(page, language, 'game-bar', [0, 1, 2, 3, 4, 5, 6].map((i) => [bar.nth(i), i + 1] as [Locator, number]), 130)
    await page.getByRole('button', { name: fr ? /^Prêts$/ : /^Ready$/ }).click()
    await page.getByRole('button', { name: fr ? /Commencer le round 1/ : /Start round 1/ }).click()

    // An adventurer's turn, then an enemy's: the initiative is shuffled, so the turns are walked until each is seen
    const next = page.locator('button:not([disabled])', { hasText: fr ? 'Tour suivant' : 'Next turn' }).first()
    const question = page.getByRole('button', { name: fr ? 'Oui' : 'Yes', exact: true })
    let adventurer = false
    let enemy = false
    for (let i = 0; i < 6 && !(adventurer && enemy); i++) {
      await page.waitForTimeout(500)
      if (await question.count()) {
        if (!enemy) {
          await shot(page, language, 'enemy-turn', [
            [page.getByText(fr ? /adversaire est-il adjacent/ : /opponent adjacent/).first(), 1],
            [page.getByText(fr ? /Suggestion/ : /Suggestion/).first(), 2],
            [question, 3],
          ])
          enemy = true
        }
        await page.getByRole('button', { name: fr ? 'Non' : 'No', exact: true }).first().click()
        continue
      }
      if (!adventurer && (await page.getByText(fr ? /Tour de (Mira|Tobin)/ : /(Mira|Tobin)’s turn/).count())) {
        await shot(page, language, 'adventurer-turn', [
          [page.getByText(fr ? 'DEVANT' : 'FRONT').locator('..'), 1],
          [page.getByText(fr ? /Tour de (Mira|Tobin)/ : /(Mira|Tobin)’s turn/).first(), 2],
          [page.getByText(fr ? /Déplacement 1/ : /Move 1/).first().locator('..'), 3],
          [next, 4],
        ])
        adventurer = true
      }
      if (await next.count()) await next.click()
    }

    // The room: the players' phones
    await bar.nth(2).click()
    await page.getByRole('button', { name: fr ? /Ouvrir la room/ : /Open the room/ }).click()
    const dialog = page.getByRole('dialog')
    const code = dialog.locator('img, canvas, svg:not(.p-icon)').first()
    await code.waitFor()
    await shot(page, language, 'room', [
      [code, 1],
      [dialog.getByRole('button', { name: fr ? /Répartir/ : /Share out/ }), 2],
      [dialog.getByText(fr ? /Demandes des joueurs/ : /Players' requests/), 3],
      [dialog.getByText(fr ? /Ouvrir comme joueur/ : /Open as a player/), 4],
    ])
    await page.keyboard.press('Escape')

    // Workshop
    await page.goto('/workshop')
    await shot(page, language, 'workshop', [
      [button('Importer', 'Import'), 1],
      [button('Nouveau scénario', 'New scenario'), 2],
      [page.getByRole('button', { name: fr ? /^Dupliquer/ : /^Duplicate/ }), 3],
    ])
    await page.getByRole('button', { name: fr ? /^Dupliquer/ : /^Duplicate/ }).first().click()
    const graph = page.getByRole('application', { name: fr ? 'Graphe du scénario' : 'Scenario graph' })
    await graph.waitFor()
    await shot(page, language, 'editor', [[graph, 1]])

    // Back: the copy is removed
    await page.goto('/workshop')
    await page
      .getByRole('region', { name: fr ? 'Mes scénarios' : 'My scenarios' })
      .getByRole('button', { name: fr ? /^Supprimer/ : /^Delete/ })
      .first()
      .click()
    await page.getByRole('button', { name: fr ? 'Supprimer' : 'Delete', exact: true }).click()
  })
}
