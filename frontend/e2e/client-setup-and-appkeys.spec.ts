/** @requirement UI-123 */

import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('Client Setup & App Key Management Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement AUTH-02
   * @category AUTH
   * @type PositiveFeature
   * @description should open App Keys & Security view and display client setup controls
   */
  test('should open App Keys & Security view and display client setup controls', async ({ page }) => {
    await page.goto('/');

    // Click App Keys & Security button in top nav
    const securityTab = page.locator('[data-testid="tab-security"], button:has-text("App Keys & Security")').first();
    await expect(securityTab).toBeVisible();
    await securityTab.click();

    // Verify Security View opens
    const securityView = page.locator('#view-security, [data-testid="appkeys-card"]').first();
    await expect(securityView).toBeVisible();

    // Check Create App Key button
    const generateKeyBtn = page.locator('[data-testid="btn-create-appkey"], button:has-text("Create App Key")').first();
    await expect(generateKeyBtn).toBeVisible();
  });

});
