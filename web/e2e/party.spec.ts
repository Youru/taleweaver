import { expect, test } from '@playwright/test'

test('a player chooses on their phone, the GM sets out', async ({ page, browser }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Jouer Blight of Rhamsted' }).click()
  await page.getByRole('button', { name: 'Préparer le groupe' }).click()
  await expect(page.getByRole('heading', { name: 'Préparer le groupe' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Partir à l’aventure' })).toBeDisabled()

  const game = page.url().split('/').pop()!
  const room = await (await page.request.post(`/api/games/${game}/room`)).json()
  const context = await browser.newContext({
    viewport: { width: 375, height: 812 },
    baseURL: new URL(page.url()).origin,
    locale: 'fr-FR',
  })
  const phone = await context.newPage()
  await phone.goto(`/join/${room.code}`)
  await phone.getByRole('textbox').last().fill('Léa')
  await phone.locator('button[type="submit"]').click()
  await expect(phone.getByRole('heading', { name: 'Choisis ton aventurier' })).toBeVisible()
  await phone.getByRole('button', { name: 'Rook' }).click()
  await expect(phone.getByText('Tu es prêt ? Le MJ donne le départ.')).toBeVisible()

  await page.reload()
  await expect(page.getByRole('heading', { name: 'Rook' })).toBeVisible()
  await page.getByRole('button', { name: 'Partir à l’aventure' }).click()

  await expect(page.getByRole('button', { name: 'Continuer' })).toBeVisible()
  await expect(phone.getByRole('heading', { name: 'Choisis ton aventurier' })).toHaveCount(0)
  await expect(phone.getByRole('tab', { name: 'Journal' })).toBeVisible()
  await context.close()
})

test('Act 1 sets out with its four adventurers and their packs', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: /^Jouer Unintentional Malum/ }).click()
  await page.getByRole('button', { name: 'Préparer le groupe' }).click()

  await expect(page.getByText('Paquetage de Nightingale')).toBeVisible()
  await expect(page.getByText('Paquetage de Rook')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Partir à l’aventure' })).toBeEnabled()
})
