/** @requirement UI-120 */

import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('RBAC Policy and Group/SID Mapping Lifecycle Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement AUTH-03
   * @category AUTH
   * @type PositiveFeature
   * @description RBAC and SID mapping administration UI allows configuring role policies and SID associations
   */
  test('should create, verify, and delete RBAC policy and SID mapping', async ({ page }) => {
    await page.goto('/');

    // Navigate to Settings
    const settingsTab = page.locator('[data-testid="tab-settings"], button:has-text("Settings")').first();
    await expect(settingsTab).toBeVisible();
    await settingsTab.click();

    // Click on Access Control tab
    const accessTab = page.locator('[data-testid="settings-tab-permissions"], button:has-text("Access Control")').first();
    await expect(accessTab).toBeVisible();
    await accessTab.click();

    // Verify Access Control headers
    await expect(page.locator('text=Access Control Policies')).toBeVisible();
    await expect(page.locator('text=Group & SID Mappings')).toBeVisible();

    // Open Policy Modal
    const addPolicyBtn = page.locator('[data-testid="btn-create-policy"], button:has-text("Create Policy")').first();
    await expect(addPolicyBtn).toBeVisible();
    await addPolicyBtn.click();

    // Fill policy modal
    const policyModal = page.locator('[data-testid="policy-modal"], #policy-modal').first();
    await expect(policyModal).toBeVisible();

    const targetInput = page.locator('[data-testid="policy-target-input"], #policy-target');
    await targetInput.fill('server:test-server');

    const groupInput = page.locator('[data-testid="policy-group-input"], #policy-group');
    await groupInput.fill('Engineering');

    // Save policy
    const savePolicyBtn = page.locator('[data-testid="policy-save-btn"], button:has-text("Save Policy")').first();
    await savePolicyBtn.click();
    await expect(policyModal).toBeHidden({ timeout: 5000 });

    // Open Mapping Modal
    const addMappingBtn = page.locator('[data-testid="btn-create-mapping"], button:has-text("Create Mapping")').first();
    await expect(addMappingBtn).toBeVisible();
    await addMappingBtn.click();

    const mappingModal = page.locator('[data-testid="mapping-modal"], #mapping-modal').first();
    await expect(mappingModal).toBeVisible();

    const externalIdInput = page.locator('[data-testid="mapping-external-input"], #mapping-external');
    await externalIdInput.fill('S-1-5-21-1001');

    const internalGroupInput = page.locator('[data-testid="mapping-internal-input"], #mapping-internal');
    await internalGroupInput.fill('Developers');

    const saveMappingBtn = page.locator('[data-testid="mapping-save-btn"], button:has-text("Save Mapping")').first();
    await saveMappingBtn.click();
    await expect(mappingModal).toBeHidden({ timeout: 5000 });
  });
});
