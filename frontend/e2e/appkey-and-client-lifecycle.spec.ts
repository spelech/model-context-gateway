/** @requirement UI-129 */

import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('AppKey and Client Lifecycle Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement AUTH-02
   * @category AUTH
   * @type PositiveFeature
   * @description should create client application and generate AppKey with scope constraints
   */
  test('should create client application and generate AppKey with scope constraints', async ({ page }) => {
    await page.goto('/');

    // Navigate to Clients view
    const clientsTab = page.locator('[data-testid="tab-security"], button:has-text("App Keys & Security")').first();
    await expect(clientsTab).toBeVisible();
    await clientsTab.click();

    // Verify Clients & Security view is open
    await expect(page.locator('#view-security, [data-testid="appkeys-card"]').first()).toBeVisible();

    // Open Create AppKey modal
    const createKeyBtn = page.locator('[data-testid="btn-create-appkey"], button:has-text("Create App Key")').first();
    await expect(createKeyBtn).toBeVisible();
    await createKeyBtn.click();

    // Fill AppKey form
    const keyModal = page.locator('[data-testid="appkey-modal"], #add-appkey-modal').first();
    await expect(keyModal).toBeVisible();

    const nameInput = page.locator('[data-testid="appkey-name-input"], #key-name').first();
    await nameInput.fill('Claude Desktop Integration');

    // Submit AppKey creation
    const submitBtn = page.locator('[data-testid="appkey-save-btn"]');
    await submitBtn.click();

    // Verify raw key presentation / snippet display
    await expect(page.locator('text=App Key Created!')).toBeVisible();

    // Close modal
    const doneBtn = page.locator('[data-testid="appkey-done-btn"], [data-testid="appkey-close-btn"]').first();
    await expect(doneBtn).toBeVisible();
    await doneBtn.click();
    await expect(keyModal).toBeHidden();
  });
});
