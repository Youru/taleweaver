import { expect, test } from '@playwright/test'

test('a question in French opens the rule, and its references lead on', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'Règles' }).click()
  await page.getByRole('searchbox').fill('que se passe-t-il quand on meurt')
  const rule = page.getByRole('article')
  await expect(rule.getByRole('heading', { level: 2 })).toHaveText('Being Defeated')
  await expect(rule.locator('.answer')).toBeVisible()

  await rule.getByRole('button', { name: /Pg\. 14/ }).click()
  await expect(rule.getByRole('heading', { level: 2 })).toHaveText(/Setting up/i)
  await rule.getByRole('button', { name: 'Retour', exact: true }).click()
  await expect(rule.getByRole('heading', { level: 2 })).toHaveText('Being Defeated')
})

test('the rules open over another screen and keep the last question', async ({ page }) => {
  await page.goto('/workshop')
  await expect(page.getByRole('link', { name: 'Règles' })).toBeVisible()
  await page.keyboard.press('Control+k')
  const panel = page.getByRole('dialog')
  await expect(panel.getByRole('searchbox')).toBeFocused()
  await page.keyboard.type('ligne de vue')
  await expect(panel.getByRole('article').getByRole('heading', { level: 2 })).toHaveText('Line of Sight')

  await page.keyboard.press('Escape')
  await expect(panel).toBeHidden()
  await expect(page).toHaveURL(/workshop$/)
  await page.keyboard.press('Control+k')
  await expect(panel.getByRole('searchbox')).toHaveValue('ligne de vue')
})
