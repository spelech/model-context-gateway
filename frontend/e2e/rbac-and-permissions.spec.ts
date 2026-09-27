/** @requirement UI-127 */

import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('RBAC Access Control & Policy Modal Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement AUTH-01
   * @category AUTH
   * @type PositiveFeature
   * @description should navigate to settings permissions tab and open policy configuration modal
   */
  test('should navigate to settings permissions tab and open policy configuration modal', async ({ page }) => {
    await page.goto('/');

    // Navigate to Settings
    const settingsTab = page.locator('[data-testid="tab-settings"], button:has-text("Settings")').first();
    await expect(settingsTab).toBeVisible();
    await settingsTab.click();

    // Click Permissions & Policies sub-tab
    const permissionsSubTab = page.locator('[data-testid="settings-tab-permissions"], button:has-text("Access Control")').first();
    await expect(permissionsSubTab).toBeVisible();
    await permissionsSubTab.click();

    // Check Create Policy button
    const addPolicyBtn = page.locator('[data-testid="btn-create-policy"], button:has-text("Create Policy")').first();
    await expect(addPolicyBtn).toBeVisible();
    await addPolicyBtn.click();

    // Verify Policy Modal opens
    const policyModal = page.locator('[data-testid="policy-modal"], #policy-modal').first();
    await expect(policyModal).toBeVisible();

    // Check target input
    const targetInput = page.locator('[data-testid="policy-target-input"], input#policy-target');
    await expect(targetInput).toBeVisible();
    await targetInput.fill('server:ha');

    // Check group input
    const groupInput = page.locator('[data-testid="policy-group-input"], input#policy-group');
    await expect(groupInput).toBeVisible();
    await groupInput.fill('SmartHomeOperators');

    // Close modal
    const closeBtn = page.locator('[data-testid="policy-cancel-btn"], [data-testid="policy-close-btn"]').first();
    await closeBtn.click();
    await expect(policyModal).toBeHidden();
  });

});
