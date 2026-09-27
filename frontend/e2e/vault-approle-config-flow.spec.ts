import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('Vault AppRole Configuration Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement SEC-02
   * @category SEC
   * @type PositiveFeature
   * @description Configure Vault AppRole credentials and test connection in settings.
   */
  test('should configure Vault AppRole credentials and test connection in settings', async ({ page }) => {
    await page.goto('/');

    // Navigate to Settings -> Secret Providers
    const settingsTab = page.locator('[data-testid="tab-settings"], button:has-text("Settings")').first();
    await expect(settingsTab).toBeVisible();
    await settingsTab.click();

    const providersTab = page.locator('[data-testid="settings-tab-secrets"], button:has-text("Secret Providers")').first();
    await expect(providersTab).toBeVisible();
    await providersTab.click();

    // Check Vault card header
    await expect(page.locator('h2:has-text("Secret Providers")').first()).toBeVisible();

    const vaultSwitch = page.locator('#sec-vault-enabled');
    if (!(await vaultSwitch.isChecked())) {
      await page.locator('label:has(#sec-vault-enabled) .slider').click();
    }
    await expect(vaultSwitch).toBeChecked();

    // Select AppRole radio
    const appRoleRadio = page.locator('input[value="approle"]');
    await expect(appRoleRadio).toBeVisible();
    await appRoleRadio.click();

    // Fill Role ID and Secret ID
    const roleIdInput = page.locator('input[placeholder="Role ID"]');
    await expect(roleIdInput).toBeVisible();
    await roleIdInput.fill('test-role-id');

    const secretIdInput = page.locator('input[placeholder="Secret ID"]');
    await expect(secretIdInput).toBeVisible();
    await secretIdInput.fill('test-secret-id');

    // Click Test Vault button
    const testVaultBtn = page.locator('[data-testid="btn-test-vault"], #btn-test-vault');
    await expect(testVaultBtn).toBeVisible();
    await testVaultBtn.click();

    // Verify feedback
    const feedback = page.locator('[data-testid="vault-test-feedback"], #vault-test-feedback');
    await expect(feedback).toBeVisible();

    // Save Secret config
    const saveSecretsBtn = page.locator('[data-testid="btn-save-secrets"], #btn-save-secrets');
    await expect(saveSecretsBtn).toBeVisible();
    await saveSecretsBtn.click();
  });
});
