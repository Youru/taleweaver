import { expect, test } from '@playwright/test'

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

  await page.getByRole('link', { name: 'Atelier' }).first().click()
  const mine = page.getByRole('region', { name: 'Mes scénarios' })
  await mine.getByRole('button', { name: /^Supprimer Blight of Rhamsted/ }).click()
  await page.getByRole('button', { name: 'Supprimer', exact: true }).click()
  await expect(mine.getByText(/^Blight of Rhamsted/)).toHaveCount(0)
})
