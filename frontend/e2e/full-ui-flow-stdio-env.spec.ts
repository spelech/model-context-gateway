import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';
import { DashboardPage } from './pages/DashboardPage';
import { ServerModalPage } from './pages/ServerModalPage';
import { TestBenchPage } from './pages/TestBenchPage';

test.describe('Full UI Flow: STDIO Transport + Env Variable Secret Provider', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement TRANS-02
   * @category TRANS
   * @type PositiveFeature
   * @description Register STDIO server with Env provider, verify connection card, and execute tool via Test Bench.
   */
  test('should register STDIO server, verify card, and execute echo tool via Test Bench', async ({ page }) => {
    const dashboard = new DashboardPage(page);
    const serverModal = new ServerModalPage(page);
    const testbench = new TestBenchPage(page);

    // 1. Open Dashboard
    await dashboard.goto();

    // 2. Open Add Server Modal
    await expect(dashboard.addServerBtn).toBeVisible();
    await dashboard.addServerBtn.click();
    await expect(serverModal.modal).toBeVisible();

    // 3. Fill STDIO server details with Env provider
    await serverModal.fillServerForm({
      id: 'stdio_env_mock',
      name: 'STDIO Env Mock',
      type: 'stdio',
      url: 'node mock_stdio.js',
      secretProvider: 'Environment',
      secretKey: 'TEST_API_KEY'
    });

    // 4. Save server
    await serverModal.save();

    // 5. Assert server card appears on dashboard
    await dashboard.searchServer('STDIO Env Mock');
    const serverItem = page.locator('.server-item, .server-card').filter({ hasText: 'STDIO Env Mock' }).first();
    await expect(serverItem).toBeVisible({ timeout: 15000 });

    // 6. Navigate to Test Bench
    await dashboard.navigateToTestbench();
    await expect(page.locator('#view-testbench')).toBeVisible();

    // 7. Select our STDIO Env Mock server and call the echo tool
    await testbench.selectServerAndTool('stdio_env_mock', 'echo');

    // 8. Fill in the message argument dynamically generated in the form
    const messageInput = page.locator('[data-testid="param-input-message"], #param-message');
    await expect(messageInput).toBeVisible();
    await messageInput.fill('hello stdio from e2e');

    // 9. Execute the tool
    await testbench.executeTool();

    // 10. Assert that the output console shows successful response
    await expect(testbench.outputConsole).toContainText('Tool executed successfully');
  });

});
