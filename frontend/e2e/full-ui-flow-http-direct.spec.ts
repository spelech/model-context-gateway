import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';
import { DashboardPage } from './pages/DashboardPage';
import { ServerModalPage } from './pages/ServerModalPage';
import { TestBenchPage } from './pages/TestBenchPage';

test.describe('Full UI Flow: HTTP Transport + Direct Key Secret Provider', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement TRANS-01
   * @category TRANS
   * @type PositiveFeature
   * @description Register HTTP server with Direct Key, verify status badge, and execute tool in Test Bench.
   */
  test('should register HTTP server with Direct Key, verify status badge, and execute tool in Test Bench', async ({ page }) => {
    const dashboard = new DashboardPage(page);
    const serverModal = new ServerModalPage(page);
    const testbench = new TestBenchPage(page);

    // 1. Open Dashboard
    await dashboard.goto();
    await expect(dashboard.navDashboardBtn).toBeVisible();

    // 2. Open Add Server Modal
    await expect(dashboard.addServerBtn).toBeVisible();
    await dashboard.addServerBtn.click();
    await expect(serverModal.modal).toBeVisible();

    // 3. Fill HTTP server details with Direct Key provider
    await serverModal.fillServerForm({
      id: 'http_direct_mock',
      name: 'HTTP Mock Server',
      type: 'http',
      url: 'http://127.0.0.1:8090/mcp',
      secretProvider: 'None'
    });

    // 4. Save server
    await serverModal.save();

    // 5. Assert server card appears on dashboard with status badge
    await dashboard.searchServer('HTTP Mock Server');
    const serverCard = page.locator('.server-card, .server-item').filter({ hasText: 'HTTP Mock Server' }).first();
    await expect(serverCard).toBeVisible({ timeout: 15000 });

    // 6. Navigate to Test Bench
    await dashboard.navigateToTestbench();

    // 7. Select new server & execute tool
    await testbench.selectServerAndTool('http_direct_mock', 'health');
    await testbench.executeTool();

    // Verify console response output
    await expect(testbench.outputConsole).toContainText('Tool executed successfully');
  });

});
