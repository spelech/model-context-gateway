/** @requirement UI-128 */

import { test, expect } from '@playwright/test';

test.describe('Test Bench View Flow', () => {

  /**
   * @requirement UI-128
   * @category UI
   * @type PositiveFeature
   * @description should navigate to Test Bench view, render tester cards, and test semantic search
   */
  test('should navigate to Test Bench view and render tester cards deterministically', async ({ page }) => {
    await page.goto('/');

    // Switch to Test Bench view
    const testbenchTab = page.locator('[data-testid="tab-testbench"]');
    await expect(testbenchTab).toBeVisible();
    await testbenchTab.click();

    // Verify Test Bench view is displayed
    await expect(page.locator('[data-testid="view-testbench"]')).toBeVisible();

    // Verify Tool Tester Card and controls
    await expect(page.locator('[data-testid="tool-tester-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="tool-server-select"]')).toBeVisible();
    await expect(page.locator('[data-testid="tool-name-select"]')).toBeVisible();

    // Verify Console Card
    await expect(page.locator('[data-testid="console-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="console-request-output"]')).toBeVisible();
    await expect(page.locator('[data-testid="console-response-output"]')).toBeVisible();

    // Verify Semantic Router Search Card and execute query
    await expect(page.locator('[data-testid="semantic-router-card"]')).toBeVisible();
    const searchInput = page.locator('[data-testid="semantic-query-input"]');
    await expect(searchInput).toBeVisible();
    await searchInput.fill('restart container');
    await expect(searchInput).toHaveValue('restart container');
    await page.locator('[data-testid="semantic-search-btn"]').click();

    // Switch to Prompts tab and verify
    await page.locator('[data-testid="testbench-tab-prompts"]').click();
    await expect(page.locator('[data-testid="prompt-tester-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="prompt-server-select"]')).toBeVisible();

    // Switch to Resources tab and verify
    await page.locator('[data-testid="testbench-tab-resources"]').click();
    await expect(page.locator('[data-testid="resource-tester-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="resource-server-select"]')).toBeVisible();
  });

});
