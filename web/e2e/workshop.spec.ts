import { expect, test, type Page } from '@playwright/test'

test('a GM creates a scenario in the workshop, branches it on the graph, and tests it', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'Atelier' }).click()
  await page.getByRole('button', { name: 'Nouveau scénario' }).click()
  await page.getByLabel('Titre du scénario').fill('La Prime du Marais')
  await page.getByRole('button', { name: 'Créer' }).click()
  await expect(page).toHaveURL(/\/workshop\/la-prime-du-marais$/)
  await expect(page.getByRole('heading', { name: 'La Prime du Marais' })).toBeVisible()

  // The first scene, then an encounter added from the bar
  const graph = page.getByRole('application', { name: 'Graphe du scénario' })
  await graph.getByText('Introduction', { exact: true }).click()
  const sheet = page.getByRole('region', { name: 'Fiche de intro' })
  await sheet.getByLabel('Texte lu aux joueurs').fill('Le marais sent la vase et le danger.')
  await page.getByRole('button', { name: 'Rencontre', exact: true }).click()
  const fight = page.getByRole('region', { name: 'Fiche de encounter-1' })
  await fight.getByLabel('Nom de la rencontre').fill('Embuscade dans les roseaux')
  await expect(graph.getByText('Embuscade dans les roseaux')).toBeVisible()

  // Nothing leads to the encounter yet: the scene's "next" goes there
  await graph.getByText('Introduction', { exact: true }).click()
  await sheet.getByRole('combobox', { name: 'Suite' }).click()
  await page.getByRole('option', { name: 'Rencontre · Embuscade dans les roseaux' }).click()
  await expect(graph.locator('.vue-flow__edge')).toHaveCount(3)

  // A choice: option 1 keeps the encounter, option 2 is pulled on the graph to the defeat
  await sheet.getByText('Les joueurs choisissent la suite').click()
  await sheet.getByLabel('Libellé de l’option 2').fill('Rebrousser chemin')
  const handle = graph.locator('.vue-flow__handle[data-handleid="option:1"]')
  const defeat = graph.locator('.vue-flow__node-ending').filter({ hasText: 'Fin : défaite' })
  const from = (await handle.boundingBox())!
  const to = (await defeat.boundingBox())!
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2)
  await page.mouse.down()
  // Dropped on the ending, near where links come in (its left side)
  await page.mouse.move(to.x + 20, to.y + to.height / 2, { steps: 12 })
  await page.mouse.up()
  await expect(graph.locator('.vue-flow__edge')).toHaveCount(4)
  await expect(sheet.getByRole('combobox', { name: 'Suite de l’option 2' })).toHaveText('Fin : défaite')

  // Saved, checked, then played
  await expect(page.getByText('Aucun problème')).toBeVisible()
  await page.getByRole('button', { name: 'Enregistrer' }).click()
  await expect(page.getByRole('button', { name: 'Enregistré' })).toBeDisabled()
  await page.getByRole('button', { name: 'Tester' }).click()
  await page.getByLabel('Aventurier 1').fill('Nightingale')
  await page.getByRole('button', { name: 'Commencer' }).click()
  await expect(page).toHaveURL(/\/games\/\w+/)
  await expect(page.getByText('Le marais sent la vase et le danger.')).toBeVisible()
  // The scene asks its question right away; option 1 leads to the encounter
  await expect(page.getByText('Que font les aventuriers ?')).toBeVisible()
  await page.getByRole('button', { name: 'Option 1' }).click()
  await expect(page.getByRole('heading', { name: 'Embuscade dans les roseaux' })).toBeVisible()
})

test('an official scenario is read only: the workshop duplicates it', async ({ page }) => {
  await page.goto('/workshop')
  const official = page.getByRole('region', { name: 'Scénarios officiels' })
  await expect(official.getByText('lecture seule')).toBeVisible()
  await official.getByRole('button', { name: 'Dupliquer' }).first().click()
  await expect(page).toHaveURL(/\/workshop\/blight-of-rhamsted-copie$/)
  // Every step of the original is on the graph, with its branches
  const graph = page.getByRole('application', { name: 'Graphe du scénario' })
  await expect(graph.locator('.vue-flow__node')).toHaveCount(4 + 3)
  await expect(page.getByText('Aucun problème')).toBeVisible()
  // Playable, with points to check: more Fragors than cards, a loot token never laid…
  await expect(page.getByText('3 avertissements')).toBeVisible()

  await page.getByRole('link', { name: 'Atelier' }).first().click()
  const mine = page.getByRole('region', { name: 'Mes scénarios' })
  await mine.getByRole('button', { name: /^Supprimer Blight of Rhamsted/ }).click()
  await page.getByRole('button', { name: 'Supprimer', exact: true }).click()
  await expect(mine.getByText(/^Blight of Rhamsted/)).toHaveCount(0)
})

/** The middle of a square of the map editor's board, on screen. */
async function square(page: Page, x: number, y: number) {
  return page.getByRole('application', { name: 'Carte de la rencontre' }).evaluate(
    (svg: SVGSVGElement, [cx, cy]) => {
      const point = new DOMPoint((cx! + 0.5) * 100, (cy! + 0.5) * 100).matrixTransform(svg.getScreenCTM()!)
      return { x: point.x, y: point.y }
    },
    [x, y],
  )
}

test('a GM draws the map of an encounter: tiles, tokens, enemies and the start, then plays it', async ({ page }) => {
  await page.goto('/workshop')
  await page.getByRole('button', { name: 'Nouveau scénario' }).click()
  await page.getByLabel('Titre du scénario').fill('La Crypte Oubliée')
  await page.getByRole('button', { name: 'Créer' }).click()
  const graph = page.getByRole('application', { name: 'Graphe du scénario' })
  // The new encounter is selected: its sheet opens the map editor
  await page.getByRole('button', { name: 'Rencontre', exact: true }).click()
  await page.getByRole('button', { name: 'Éditer la carte' }).click()

  // A tile, laid on the first square
  const palette = page.getByRole('complementary', { name: 'Pièces' })
  await palette.getByPlaceholder('Filtrer').fill('UM47')
  await palette.getByRole('button', { name: 'Poser la tuile UM47' }).click()
  let at = await square(page, 0, 0)
  await page.mouse.click(at.x, at.y)
  const board = page.getByRole('application', { name: 'Carte de la rencontre' })
  await expect(board.getByRole('button', { name: 'Tuile UM47 en 0, 0' })).toBeVisible()

  // A blue objective, then an enemy
  await palette.getByRole('button', { name: 'Jetons', exact: true }).click()
  await palette.getByPlaceholder('Filtrer').fill('objective')
  await palette.getByRole('button', { name: 'Poser le jeton Objective · bleu' }).click()
  at = await square(page, 3, 2)
  await page.mouse.click(at.x, at.y)
  await palette.getByRole('button', { name: 'Ennemis', exact: true }).click()
  await palette.getByPlaceholder('Filtrer').fill('fragor')
  await palette.getByRole('button', { name: 'Poser un ennemi : Fragor' }).click()
  at = await square(page, 4, 1)
  await page.mouse.click(at.x, at.y)
  await expect(board.getByRole('button', { name: 'Ennemi Fragor en 4, 1' })).toBeVisible()

  // The starting zone, drawn by dragging along the bottom row
  await palette.getByRole('button', { name: 'Zones', exact: true }).click()
  await palette.getByRole('button', { name: 'Zone de départ' }).click()
  const from = await square(page, 0, 5)
  const to = await square(page, 3, 5)
  await page.mouse.move(from.x, from.y)
  await page.mouse.down()
  await page.mouse.move(to.x, to.y, { steps: 8 })
  await page.mouse.up()
  const inspector = page.getByRole('complementary', { name: 'Pièce sélectionnée' })
  await expect(inspector.getByLabel('Largeur')).toHaveValue('4')

  // The enemy is moved by the keyboard
  await board.getByRole('button', { name: 'Ennemi Fragor en 4, 1' }).click()
  await page.keyboard.press('ArrowDown')
  await expect(board.getByRole('button', { name: 'Ennemi Fragor en 4, 2' })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(inspector.getByText('1 tuile')).toBeVisible()
  await expect(inspector.getByRole('combobox', { name: 'Départ des aventuriers' })).toContainText('start')
  await page.screenshot({ path: test.info().outputPath('map-editor.png') })

  // Back to the graph, saved, then played: the board is the one drawn
  await page.getByRole('button', { name: 'Retour au graphe' }).click()
  await expect(page.getByText('1 tuile · 1 jeton · 1 ennemi')).toBeVisible()

  // The objective reveals a hidden text and gives gold
  await page.getByRole('tab', { name: 'Jetons' }).click()
  const objective = page.getByRole('region', { name: 'Jeton objective:blue' })
  await objective.getByRole('combobox', { name: 'Activation' }).click()
  await page.getByRole('option', { name: 'En entrant sur sa case' }).click()
  await objective.getByRole('button', { name: 'Ajouter un effet à ce jeton' }).click()
  await objective.getByLabel('Texte caché (lu à voix haute)').fill('Un coffre poussiéreux contient 15 pièces.')
  await objective.getByRole('combobox', { name: /^Ajouter une action/ }).click()
  await page.getByRole('option', { name: 'Gagner de l’or' }).click()
  await objective.getByRole('spinbutton', { name: 'Quantité' }).fill('15')
  await objective.getByRole('spinbutton', { name: 'Quantité' }).blur()
  await page.screenshot({ path: test.info().outputPath('token-rules.png'), fullPage: true })
  await page.getByRole('tab', { name: 'Fiche' }).click()
  await graph.getByText('Introduction', { exact: true }).click()
  await page.getByRole('combobox', { name: 'Suite' }).click()
  await page.getByRole('option', { name: /^Rencontre · / }).click()
  await expect(page.getByText('Aucun problème')).toBeVisible()
  await page.getByRole('button', { name: 'Tester' }).click()
  await page.getByLabel('Aventurier 1').fill('Nightingale')
  await page.getByRole('button', { name: 'Commencer' }).click()
  await page.getByRole('button', { name: 'Continuer' }).click()
  const table = page.getByRole('img', { name: /^Plateau : / })
  await expect(table.locator('g.tile image')).toHaveCount(1)
  await expect(table.locator('g.figure.combatant')).toHaveCount(1)
  await expect(table.locator('g.figure.adventurer')).toHaveCount(1)
  await table.getByRole('button', { name: 'Jeton objective:blue' }).click()
  await page.getByRole('button', { name: 'Activer' }).click()
  await expect(page.getByText('15 or', { exact: false }).first()).toBeVisible()
  await page.getByRole('tab', { name: 'Journal' }).click()
  await expect(page.getByRole('tabpanel', { name: 'Journal' })).toContainText(
    'Un coffre poussiéreux contient 15 pièces.',
  )
})
