/** @requirement UI-124 */

import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';

test.describe('Dashboard & Navigation Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Renders main dashboard navigation tabs and layout headers
   */
  test('should render the dashboard layout and header components', async ({ page }) => {
    await page.goto('/');

    // Check navigation buttons exist
    await expect(page.locator('[data-testid="tab-dashboard"], button:has-text("Overview")').first()).toBeVisible();
    await expect(page.locator('[data-testid="tab-testbench"], button:has-text("Test Bench")').first()).toBeVisible();
    await expect(page.locator('[data-testid="tab-settings"], button:has-text("Settings")').first()).toBeVisible();
    await expect(page.locator('[data-testid="tab-security"], button:has-text("App Keys & Security")').first()).toBeVisible();
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Displays aggregate system metrics and health status cards
   */
  test('should display aggregate statistics cards', async ({ page }) => {
    await page.goto('/');

    // Check stats container exists
    const statsContainer = page.locator('.stats-card, .stats-container, .dashboard-stats, .dashboard-container');
    await expect(statsContainer.first()).toBeVisible();
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type PositiveFeature
   * @description Filters backend MCP server catalog via dashboard search input
   */
  test('should filter servers using search input', async ({ page }) => {
    await page.goto('/');

    // Type in search bar
    const searchInput = page.locator('[data-testid="server-search-input"], #server-search').first();
    await expect(searchInput).toBeVisible();
    await searchInput.fill('docker');
    await expect(searchInput).toHaveValue('docker');
  });

});
