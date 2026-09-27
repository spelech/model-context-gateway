import { test, expect } from '@playwright/test';
import { setupMockApi } from './fixtures/mockApi';
import { DashboardPage } from './pages/DashboardPage';
import { ServerModalPage } from './pages/ServerModalPage';

test.describe('My MCP Servers & User Credentials Flow', () => {

  test.beforeEach(async ({ page }) => {
    await setupMockApi(page);
  });

  /**
   * @requirement AUTH-05
   * @category AUTH
   * @type PositiveFeature
   * @description Render user-provided servers and configure per-user credentials in My MCP Servers view.
   */
  test('should render user provided servers and allow editing credentials with SQLite schema', async ({ page }) => {
    const dashboard = new DashboardPage(page);
    const serverModal = new ServerModalPage(page);
    const testServerId = `sqlite_auth_${Date.now()}`;
    const testServerName = `SQLite Auth ${Date.now()}`;

    await dashboard.goto();

    await expect(dashboard.addServerBtn).toBeVisible();
    await dashboard.addServerBtn.click();
    await expect(serverModal.modal).toBeVisible();

    await serverModal.fillServerForm({
      id: testServerId,
      name: testServerName,
      type: 'sse',
      url: 'http://127.0.0.1:8090/sse',
      secretProvider: 'UserProvided'
    });

    await serverModal.save();

    // Navigate to My MCP Servers
    const tabBtn = page.locator('[data-testid="tab-my-mcp"], button:has-text("My MCP Servers")').first();
    await expect(tabBtn).toBeVisible();
    await tabBtn.click();

    // Verify row content and Auth Missing status
    const firstRow = page.locator(`[data-testid="my-mcp-server-row-${testServerId}"], table.data-table tbody tr`).filter({ hasText: testServerName }).first();
    await expect(firstRow).toBeVisible();
    await expect(firstRow).toContainText('Auth Missing');

    // Click Edit Auth
    const editBtn = firstRow.locator(`[data-testid="btn-edit-auth-${testServerId}"], button:has-text("Edit Auth")`).first();
    await editBtn.click();

    // Verify modal overlay opens
    const modal = page.locator('[data-testid="user-auth-modal"], .modal-backdrop').first();
    await expect(modal).toBeVisible();
    await expect(modal).toContainText(`Edit Auth for ${testServerName}`);

    // Type JSON into textarea
    const textarea = modal.locator('[data-testid="user-auth-textarea"], textarea').first();
    await expect(textarea).toBeVisible();
    await textarea.fill('{\n  "apiKey": "test-key-123"\n}');

    // Save
    const saveBtn = modal.locator('[data-testid="user-auth-save-btn"], button:has-text("Save")').first();
    await saveBtn.click();

    // Verify modal closes and status updates to Auth Configured
    await expect(modal).toBeHidden();
    await expect(firstRow).toContainText('Auth Configured');
  });

});
