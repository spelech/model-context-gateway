/** @requirement UI-126 */

import { test, expect } from '@playwright/test';

test.describe('Server Inspector Modal Flow', () => {

  /**
   * @requirement UI-126
   * @category UI
   * @type PositiveFeature
   * @description should open Server Inspect Modal and inspect capabilities tabs deterministically
   */
  test('should open Server Inspect Modal and inspect capabilities tabs deterministically', async ({ page }) => {
    await page.route('**/api/servers', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'mock-notes',
            displayName: 'Notes Service',
            type: 'sse',
            url: 'http://localhost:3000/sse',
            enabled: true,
            connectionStatus: 'Connected',
            categories: ['notes'],
          },
        ]),
      });
    });

    await page.route('**/api/servers/mock-notes/inspect', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          tools: [{ name: 'read_note', description: 'Read a note' }],
          resources: [{ uri: 'notes://list', name: 'Note list' }],
          prompts: [{ name: 'summarize_note', description: 'Summarize a note' }],
        }),
      });
    });

    await page.goto('/');

    const inspectBtn = page.locator('[data-testid="server-inspect-btn-mock-notes"]');
    await expect(inspectBtn).toBeVisible();
    await inspectBtn.click();

    // Verify Inspect Modal overlay opens
    const inspectModal = page.locator('[data-testid="server-inspect-modal"]');
    await expect(inspectModal).toBeVisible();

    // Verify tools tab is active and shows read_note
    await expect(page.locator('[data-testid="inspect-tab-tools"]')).toBeVisible();
    await expect(inspectModal.locator('strong:has-text("read_note")')).toBeVisible();

    // Switch to resources tab
    await page.locator('[data-testid="inspect-tab-resources"]').click();
    await expect(inspectModal.locator('strong:has-text("Note list")')).toBeVisible();

    // Switch to prompts tab
    await page.locator('[data-testid="inspect-tab-prompts"]').click();
    await expect(inspectModal.locator('strong:has-text("summarize_note")')).toBeVisible();

    // Close modal
    await page.locator('[data-testid="server-inspect-close-btn"]').click();
    await expect(inspectModal).toBeHidden();
  });

});
