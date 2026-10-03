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

    // Switch to Semantic Router Tab and execute query
    await page.locator('[data-testid="testbench-tab-semantic"]').click();
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

  /**
   * @requirement UI-141
   * @category UI
   * @type PositiveFeature
   * @description should interact with semantic router simulator, change modes and presets, and test tool transition
   */
  test('should interact with semantic router simulator, change modes and presets, and test tool transition', async ({ page }) => {
    await page.route('**/api/test/tools', async (route) => {
      return route.fulfill({
        json: [
          {
            name: 'mock-docker/docker_restart',
            description: 'Restart a running container',
            inputSchema: {
              type: 'object',
              properties: {
                container_id: { type: 'string', description: 'Target container ID' },
              },
              required: ['container_id'],
            },
          },
        ],
      });
    });

    await page.route('**/api/test/semantic-search', async (route) => {
      return route.fulfill({
        json: {
          query: 'restart container',
          mode: 'hybrid',
          denseWeight: 0.7,
          results: [
            {
              tool: { name: 'mock-docker/docker_restart', description: 'Restart a running container' },
              toolName: 'mock-docker/docker_restart',
              serverId: 'mock-docker',
              score: 0.885,
              denseScore: 0.92,
              sparseScore: 0.80,
              denseRank: 1,
              sparseRank: 1,
            },
          ],
        },
      });
    });

    await page.goto('/');

    // Switch to Test Bench view
    const testbenchTab = page.locator('[data-testid="tab-testbench"]');
    await expect(testbenchTab).toBeVisible();
    await testbenchTab.click();
    await expect(page.locator('[data-testid="view-testbench"]')).toBeVisible();

    // Switch to Semantic Router Tab
    await page.locator('[data-testid="testbench-tab-semantic"]').click();
    await expect(page.locator('[data-testid="semantic-router-card"]')).toBeVisible();

    // Test mode toggling: click semantic mode -> slider disappears
    await page.locator('[data-testid="mode-btn-semantic"]').click();
    await expect(page.locator('[data-testid="hybrid-weight-slider"]')).toBeHidden();

    // Click hybrid mode -> slider reappears
    await page.locator('[data-testid="mode-btn-hybrid"]').click();
    await expect(page.locator('[data-testid="hybrid-weight-slider"]')).toBeVisible();

    // Test preset button: click Semantic Bias -> denseWeight becomes 0.7
    await page.locator('[data-testid="preset-semantic-bias"]').click();
    await expect(page.locator('[data-testid="hybrid-weight-slider"]')).toHaveValue('0.7');

    // Fill query and execute search
    const searchInput = page.locator('[data-testid="semantic-query-input"]');
    await expect(searchInput).toBeVisible();
    await searchInput.fill('restart container');
    await expect(searchInput).toHaveValue('restart container');
    await page.locator('[data-testid="semantic-search-btn"]').click();

    // Verify score badges appear
    await expect(page.locator('[data-testid="score-total"]')).toBeVisible();
    await expect(page.locator('[data-testid="score-dense"]')).toBeVisible();
    await expect(page.locator('[data-testid="score-sparse"]')).toBeVisible();

    // Click "Test Tool" transition button
    const testToolBtn = page.locator('[data-testid="test-tool-btn-mock-docker/docker_restart"]');
    await expect(testToolBtn).toBeVisible();
    await testToolBtn.click();

    // Verify active tab switches to Tools card with selected server
    await expect(page.locator('[data-testid="tool-tester-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="tool-server-select"]')).toHaveValue('mock-docker');
  });

});

