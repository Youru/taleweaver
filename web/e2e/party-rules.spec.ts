import { expect, test } from '@playwright/test'

test('a GM writes the rules of departure in the workshop, and they apply', async ({ page }) => {
  await page.goto('/workshop')
  await page.getByRole('button', { name: 'Nouveau scénario' }).click()
  await page.getByLabel('Titre du scénario').fill('Le Départ')
  await page.getByRole('button', { name: 'Créer' }).click()
  await page.getByText('Règles de départ').click()
  await page.getByRole('button', { name: 'Ajouter un paquetage' }).click()
  await page.getByLabel('Nom du paquetage').fill('Duelliste')
  await page.getByText('Ajouter un objet', { exact: true }).click()
  await page.getByRole('option', { name: 'Long Sword', exact: true }).click()
  await page.getByRole('button', { name: 'Enregistrer' }).click()
  await expect(page.getByRole('button', { name: 'Enregistré' })).toBeDisabled()

  await page.getByRole('button', { name: 'Tester' }).click()
  await page.getByRole('button', { name: 'Préparer le groupe' }).click()
  await page.getByRole('button', { name: 'Ajouter Rook' }).click()

  await expect(page.getByText('Duelliste')).toBeVisible()
  await expect(page.getByRole('img', { name: 'Long Sword' })).toBeVisible()
})
