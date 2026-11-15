import { expect, test } from '@playwright/test'

test('a GM prints Blight of Rhamsted: the booklet, an encounter page, the cards, and a PDF', async ({ page }) => {
  await page.goto('/workshop')
  await page.getByRole('button', { name: 'Imprimer Blight of Rhamsted' }).click()
  await expect(page).toHaveURL(/\/workshop\/print\?scenario=/)

  // The booklet: the cover, then each step numbered, one page per encounter
  await expect(page.getByRole('heading', { name: 'Blight of Rhamsted', level: 1 })).toBeVisible()
  const encounter = page.getByRole('article', { name: 'Page de Search for the Lair' })
  await expect(encounter).toBeVisible()
  await expect(page.getByRole('article', { name: /^Page de / })).toHaveCount(3)
  await expect(encounter.getByRole('img', { name: 'Diagramme de la rencontre' }).locator('image').first()).toBeVisible()
  await expect(encounter.getByText('Mise en place')).toBeVisible()
  await expect(encounter.getByText('UM47, UM59, UM55, UM53, UM35')).toBeVisible()
  await encounter.screenshot({ path: test.info().outputPath('encounter-page.png') })

  // The PDF, as "Save as PDF" makes it
  await page.emulateMedia({ media: 'print' })
  const pdf = await page.pdf({ preferCSSPageSize: true, printBackground: true })
  expect(pdf.subarray(0, 5).toString()).toBe('%PDF-')
  await test.info().attach('blight.pdf', { body: pdf, contentType: 'application/pdf' })
  await page.emulateMedia({ media: 'screen' })

  // One encounter alone, then the cards of the hidden texts
  await page.getByRole('combobox', { name: 'Quoi imprimer' }).click()
  await page.getByRole('option', { name: 'Rencontre · Crossing the River' }).click()
  await expect(page.getByRole('article', { name: /^Page de / })).toHaveCount(1)
  await page.getByRole('combobox', { name: 'Quoi imprimer' }).click()
  await page.getByRole('option', { name: 'Cartes des textes cachés' }).click()
  await expect(page.getByRole('article', { name: /^Carte / }).first()).toBeVisible()
  await page.screenshot({ path: test.info().outputPath('cards.png') })
})
