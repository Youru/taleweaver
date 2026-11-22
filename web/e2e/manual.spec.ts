import { expect, test } from '@playwright/test'

test('the manual opens from the home page, follows its links and the language', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'Manuel', exact: true }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Manuel du MJ' })).toBeVisible()

  const act1 = page.getByRole('heading', { level: 2, name: 'Lancer ou reprendre l’Acte 1' })
  await expect(act1).not.toBeInViewport()
  await page.getByRole('article').getByRole('link', { name: 'Lancer ou reprendre l’Acte 1' }).click()
  await expect(page).toHaveURL(/\/manual#act1$/)
  await expect(act1).toBeInViewport()

  await page.evaluate(() => window.scrollTo(0, 0))
  await page
    .getByRole('navigation', { name: 'Sommaire du manuel' })
    .getByRole('link', { name: /Acte 1/ })
    .click()
  await expect(act1).toBeInViewport()

  await page.reload()
  await expect(act1).toBeInViewport()

  await page.evaluate(() => window.scrollTo(0, 0))
  await page.getByRole('button', { name: 'EN', exact: true }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'The GM’s manual' })).toBeVisible()
})
