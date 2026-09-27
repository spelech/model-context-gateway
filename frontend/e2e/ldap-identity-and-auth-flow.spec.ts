import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('LDAP and Active Directory Identity Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement AUTH-04
   * @category AUTH
   * @type PositiveFeature
   * @description Configure LDAP / Active Directory identity provider, test connection, and save settings.
   */
  test('should configure LDAP identity provider, test connection, and save settings', async ({ page }) => {
    page.on('dialog', async (dialog) => {
      await dialog.accept();
    });

    // Navigate to Settings
    await page.goto('/');
    const settingsTab = page.locator('[data-testid="tab-settings"], button:has-text("Settings")').first();
    await expect(settingsTab).toBeVisible();
    await settingsTab.click();

    // Click on Identity & Auth tab
    const providersTab = page.locator('[data-testid="settings-tab-identity"], button:has-text("Identity & Auth")').first();
    await expect(providersTab).toBeVisible();
    await providersTab.click();

    // Verify Identity & Auth card
    await expect(page.locator('h2:has-text("Identity & Auth")').first()).toBeVisible();

    // Toggle Active Directory switch on if not enabled
    const adSwitch = page.locator('#auth-ad-enabled');
    if (!(await adSwitch.isChecked())) {
      await page.locator('label:has(#auth-ad-enabled) .slider').click();
    }
    await expect(adSwitch).toBeChecked();

    // Fill in LDAP Server details
    const serverInput = page.locator('#ad-server');
    await expect(serverInput).toBeVisible();
    await serverInput.fill(process.env.LDAP_TEST_SERVER || '127.0.0.1');

    const portInput = page.locator('#ad-port');
    await portInput.fill(process.env.LDAP_TEST_PORT || '6636');

    const domainInput = page.locator('#ad-domain');
    await domainInput.fill('corp.local');

    const baseDnInput = page.locator('#ad-base-dn');
    await baseDnInput.fill('DC=corp,DC=local');

    const bindDnInput = page.locator('#ad-bind-dn');
    await bindDnInput.fill('CN=admin,DC=corp,DC=local');

    const bindPasswordInput = page.locator('#ad-bind-password');
    await bindPasswordInput.fill('adminpassword');

    // Click Test Connection button
    const testLdapBtn = page.locator('[data-testid="btn-test-ldap"], #btn-test-ldap');
    await expect(testLdapBtn).toBeVisible();
    await testLdapBtn.click();

    // Verify feedback
    const feedback = page.locator('[data-testid="ad-test-feedback"], #ad-test-feedback');
    await expect(feedback).toBeVisible();

    // Save auth config
    const saveBtn = page.locator('[data-testid="btn-save-auth"], #btn-save-auth');
    await expect(saveBtn).toBeVisible();
    await saveBtn.click();
  });
});
