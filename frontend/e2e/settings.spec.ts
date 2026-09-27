/** @requirement UI-122 */

import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('Settings View Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type Positive
   * @description should navigate to Settings view and configure vector embedding options
   */
  test('should navigate to Settings view and configure vector embedding options', async ({ page }) => {
    await page.goto('/');

    // Click Settings button
    const settingsTab = page.locator('[data-testid="tab-settings"], button:has-text("Settings")').first();
    await expect(settingsTab).toBeVisible();
    await settingsTab.click();

    // Verify Settings view is rendered
    await expect(page.locator('[data-testid="general-tab"]')).toBeVisible();

    // Check Embedding Provider select dropdown
    const providerSelect = page.locator('select#settings-provider, select[name="embeddingProvider"]').first();
    await expect(providerSelect).toBeVisible();

    // Select Local ONNX
    await providerSelect.selectOption('local');
    await expect(providerSelect).toHaveValue('local');

    // Select External API Provider
    await providerSelect.selectOption('api');
    await expect(providerSelect).toHaveValue('api');

    // Check Security Defaults (RFC 7591)
    const dcrCheckbox = page.locator('[data-testid="settings-allow-dcr-checkbox"], #settings-allow-dcr');
    await expect(dcrCheckbox).toBeVisible();
    
    // Toggle the checkbox
    await dcrCheckbox.uncheck();
    await expect(dcrCheckbox).not.toBeChecked();

    // Check Save Settings button
    const saveBtn = page.locator('[data-testid="btn-save-general-settings"], #btn-save-settings').first();
    await expect(saveBtn).toBeVisible();
  });

});
