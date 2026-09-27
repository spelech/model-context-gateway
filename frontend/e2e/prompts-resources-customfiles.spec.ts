import { test, expect } from '@playwright/test';

test.describe('Prompts, Resources, Terminal Logs & Custom Files E2E Workflows', () => {

  /**
   * @requirement UI-04
   * @category UI
   * @type PositiveFeature
   * @description Switch tabs in Test Bench to interact with Prompt Tester and Resource Tester deterministically.
   */
  test('should interact with Prompt Tester and Resource Tester cards in Test Bench', async ({ page }) => {
    await page.goto('/');

    // Navigate to Test Bench
    const testbenchTab = page.locator('[data-testid="tab-testbench"]');
    await expect(testbenchTab).toBeVisible();
    await testbenchTab.click();

    // Switch to Prompts tab and verify
    await page.locator('[data-testid="testbench-tab-prompts"]').click();
    const promptCard = page.locator('[data-testid="prompt-tester-card"]');
    await expect(promptCard).toBeVisible();
    await expect(page.locator('[data-testid="prompt-server-select"]')).toBeVisible();
    await expect(page.locator('[data-testid="prompt-name-select"]')).toBeVisible();

    // Switch to Resources tab and verify
    await page.locator('[data-testid="testbench-tab-resources"]').click();
    const resourceCard = page.locator('[data-testid="resource-tester-card"]');
    await expect(resourceCard).toBeVisible();
    await expect(page.locator('[data-testid="resource-server-select"]')).toBeVisible();
    await expect(page.locator('[data-testid="resource-uri-input"]')).toBeVisible();

    // Verify Console Card
    await expect(page.locator('[data-testid="console-card"]')).toBeVisible();
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Navigate to Settings > Custom Files and Prompts & Resources tabs to verify authoring.
   */
  test('should navigate to Custom Files and Prompts in Settings view', async ({ page }) => {
    await page.route('**/api/me', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          username: 'admin',
          displayName: 'Administrator',
          authenticated: true,
          groups: ['full_admin']
        })
      });
    });

    await page.goto('/');

    // Navigate to Settings
    const settingsTab = page.locator('[data-testid="tab-settings"]');
    await expect(settingsTab).toBeVisible();
    await settingsTab.click();

    // Click Prompts & Resources sub-tab
    const customFilesTab = page.locator('button:has-text("Prompts & Resources"), button:has-text("Custom Files")').first();
    await expect(customFilesTab).toBeVisible();
    await customFilesTab.click();
    await expect(page.locator('button:has-text("Add Custom File"), button:has-text("Create File"), h2:has-text("Prompts & Resources")').first()).toBeVisible();
  });

});
