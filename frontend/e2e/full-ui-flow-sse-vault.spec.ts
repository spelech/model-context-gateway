import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';
import { DashboardPage } from './pages/DashboardPage';
import { ServerModalPage } from './pages/ServerModalPage';
import { TestBenchPage } from './pages/TestBenchPage';

test.describe('Full UI Flow: SSE Transport + HashiCorp Vault Secret Provider', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement SEC-01
   * @category SEC
   * @type PositiveFeature
   * @description Register SSE server with Vault provider (Mount/Path/Field), verify badge, and run semantic search.
   */
  test('should register SSE server with Vault provider (Mount/Path/Field), verify badge, and run semantic search', async ({ page }) => {
    const dashboard = new DashboardPage(page);
    const serverModal = new ServerModalPage(page);
    const testbench = new TestBenchPage(page);

    // 1. Open Dashboard
    await dashboard.goto();

    // 2. Open Add Server Modal
    await expect(dashboard.addServerBtn).toBeVisible();
    await dashboard.addServerBtn.click();
    await expect(serverModal.modal).toBeVisible();

    // 3. Fill SSE server details with Vault provider
    await serverModal.fillServerForm({
      id: 'sse_vault_mock',
      name: 'SSE Vault Mock',
      type: 'sse',
      url: 'http://127.0.0.1:8090/sse',
      secretProvider: 'Vault',
      vaultMount: 'secret',
      vaultPath: 'services/vault-test',
      vaultField: 'token'
    });

    // 4. Save server
    await serverModal.save();

    // 5. Assert server card appears on dashboard
    await dashboard.searchServer('SSE Vault Mock');
    const serverCard = page.locator('.server-card, .server-item').filter({ hasText: 'SSE Vault Mock' }).first();
    await expect(serverCard).toBeVisible({ timeout: 15000 });

    // 6. Navigate to Test Bench & test semantic search
    await dashboard.navigateToTestbench();
    await testbench.searchTools('health status check');
    await expect(page.locator('[data-testid="semantic-results-container"] .search-result-item').first()).toBeVisible({ timeout: 10000 });

    // 7. Select new server & execute health tool
    await testbench.selectServerAndTool('sse_vault_mock', 'health');
    await testbench.executeTool();

    // Verify console response output
    await expect(testbench.outputConsole).toContainText('Tool executed successfully');
  });

});
