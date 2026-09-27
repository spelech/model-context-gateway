/** @requirement MCP-36 */

import { test, expect } from '@playwright/test';

test.describe('Prompt & Resource End-to-End Execution Flow', () => {

  /**
   * @requirement MCP-36
   * @category MCP
   * @type PositiveFeature
   * @description tests interactive prompt selection, parameter input, execution, and console payload display
   */
  test('should select and execute prompt with parameters in Test Bench', async ({ page }) => {
    // Mock test prompts API
    await page.route('**/api/test/prompts', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            name: 'docker__diagnose_container',
            description: 'Run comprehensive diagnostics on container',
            arguments: [
              { name: 'container_id', description: 'Target container identifier', required: true }
            ]
          }
        ]),
      });
    });

    // Mock prompt get execution API
    await page.route('**/api/test/prompts/get', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          description: 'Diagnostics for container c-101',
          messages: [
            {
              role: 'user',
              content: {
                type: 'text',
                text: 'Please inspect container c-101 and report CPU/memory anomalies.'
              }
            }
          ]
        }),
      });
    });

    await page.goto('/');

    // Navigate to Test Bench
    const testbenchTab = page.locator('[data-testid="tab-testbench"]');
    await expect(testbenchTab).toBeVisible();
    await testbenchTab.click();

    // Switch to Prompts Tab
    const promptTab = page.locator('[data-testid="testbench-tab-prompts"]');
    await expect(promptTab).toBeVisible();
    await promptTab.click();

    // Select Server
    const serverSelect = page.locator('[data-testid="prompt-server-select"]');
    await expect(serverSelect).toBeVisible();
    await serverSelect.selectOption('docker');

    // Select Prompt
    const promptSelect = page.locator('[data-testid="prompt-name-select"]');
    await expect(promptSelect).toBeVisible();
    await promptSelect.selectOption('docker__diagnose_container');

    // Verify dynamic parameter field appears and enter argument
    const paramInput = page.locator('[data-testid="prompt-param-input-container_id"]');
    await expect(paramInput).toBeVisible();
    await paramInput.fill('c-101');

    // Click execute
    const executeBtn = page.locator('[data-testid="prompt-execute-btn"]');
    await expect(executeBtn).toBeEnabled();
    await executeBtn.click();

    // Verify Console displays request and response
    const requestOutput = page.locator('[data-testid="console-request-output"]');
    await expect(requestOutput).toContainText('prompts/get');
    await expect(requestOutput).toContainText('c-101');

    const responseOutput = page.locator('[data-testid="console-response-output"]');
    await expect(responseOutput).toContainText('Diagnostics for container c-101');
    await expect(responseOutput).toContainText('Please inspect container c-101');
  });

  /**
   * @requirement MCP-38
   * @category MCP
   * @type PositiveFeature
   * @description tests interactive resource URI selection, read execution, and console output display
   */
  test('should select and read resource in Test Bench', async ({ page }) => {
    // Mock test resources API
    await page.route('**/api/test/resources', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          resources: [
            {
              uri: 'mcp://docker/containers/status',
              name: 'Docker Containers Status',
              mimeType: 'application/json',
              description: 'Active container status summaries'
            }
          ],
          templates: []
        }),
      });
    });

    // Mock resource read execution API
    await page.route('**/api/test/resources/read', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          contents: [
            {
              uri: 'mcp://docker/containers/status',
              mimeType: 'application/json',
              text: '{"running": 12, "paused": 0, "stopped": 1}'
            }
          ]
        }),
      });
    });

    await page.goto('/');

    // Navigate to Test Bench
    await page.locator('[data-testid="tab-testbench"]').click();

    // Switch to Resources Tab
    await page.locator('[data-testid="testbench-tab-resources"]').click();

    // Select Server
    const serverSelect = page.locator('[data-testid="resource-server-select"]');
    await expect(serverSelect).toBeVisible();
    await serverSelect.selectOption('docker');

    // Select Resource
    const resourceSelect = page.locator('[data-testid="resource-name-select"]');
    await expect(resourceSelect).toBeVisible();
    await resourceSelect.selectOption('mcp://docker/containers/status');

    // Verify URI input is populated
    const uriInput = page.locator('[data-testid="resource-uri-input"]');
    await expect(uriInput).toHaveValue('mcp://docker/containers/status');

    // Click Read Resource
    const readBtn = page.locator('[data-testid="resource-read-btn"]');
    await expect(readBtn).toBeEnabled();
    await readBtn.click();

    // Verify Console displays request and response
    const requestOutput = page.locator('[data-testid="console-request-output"]');
    await expect(requestOutput).toContainText('resources/read');
    await expect(requestOutput).toContainText('mcp://docker/containers/status');

    const responseOutput = page.locator('[data-testid="console-response-output"]');
    await expect(responseOutput).toContainText('running');
  });

});
