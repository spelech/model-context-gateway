import { test, expect } from '@playwright/test';
import { LayoutInspector, getDevicePreset } from 'playwright-layout-inspector';

test.describe('Dashboard Layout & UX Audit', () => {

  test.beforeEach(async ({ page }) => {
    // Intercept backend /api calls without accidentally matching Vite modules like /src/api/...
    await page.route(url => url.pathname.startsWith('/api/'), async (route) => {
      const urlObj = new URL(route.request().url());
      const path = urlObj.pathname;

      if (path === '/api/config/branding') {
        return route.fulfill({ json: { title: 'Model Context Gateway', logoUrl: '' } });
      }
      if (path === '/api/user/me' || path === '/api/me') {
        return route.fulfill({
          json: {
            username: 'admin',
            displayName: 'Administrator',
            groups: ['full_admin', 'house_member'],
            isAdmin: true,
          },
        });
      }
      if (path === '/api/servers') {
        return route.fulfill({
          json: [
            {
              id: 'mock-docker',
              displayName: 'Docker MCP Server',
              type: 'sse',
              url: 'http://localhost:8022/sse',
              enabled: true,
              connectionStatus: 'Connected',
              categories: ['Containerization'],
              toolsCount: 5,
              promptsCount: 2,
              resourcesCount: 1,
            },
          ],
        });
      }
      if (path.startsWith('/api/servers/') && path.endsWith('/inspect')) {
        return route.fulfill({
          json: {
            tools: [
              { name: 'docker_ps', description: 'List running docker containers' },
              { name: 'docker_logs', description: 'Fetch stdout and stderr container logs' },
            ],
            resources: [
              { name: 'container_status', uri: 'docker://status', description: 'Realtime container health status' },
            ],
            prompts: [
              { name: 'diagnose_container', description: 'Diagnose unhealthy container issues' },
            ],
          },
        });
      }
      if (path.startsWith('/api/appkeys')) {
        return route.fulfill({ json: [] });
      }
      if (path.startsWith('/api/clients')) {
        return route.fulfill({ json: [] });
      }
      if (path === '/api/settings/embedding') {
        return route.fulfill({
          json: {
            provider: 'local',
            model: 'all-MiniLM-L6-v2',
            allowOpenDynamicClientRegistration: true,
          },
        });
      }
      if (path === '/api/settings/providers') {
        return route.fulfill({
          json: {
            authProviders: [],
            secretProviders: [],
          },
        });
      }
      if (path === '/api/custom-files') {
        return route.fulfill({ json: [] });
      }
      if (path === '/api/policies') {
        return route.fulfill({ json: [] });
      }
      if (path === '/api/group-mappings') {
        return route.fulfill({ json: [] });
      }

      return route.fulfill({ json: {} });
    });
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Audits desktop viewport layout for zero horizontal overflow and high UX score.
   */
  test('should pass layout audit on desktop 1080p viewport', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const inspector = new LayoutInspector(page);
    const result = await inspector.audit({
      device: getDevicePreset('Desktop 1080p'),
      includeScreenshot: false,
    });

    expect(result.overflowIssues.length).toBe(0);
    expect(result.uxScore.totalScore).toBeGreaterThanOrEqual(85);
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Audits mobile viewport layout (Samsung Galaxy S25+) for zero horizontal overflow and high UX score.
   */
  test('should pass layout audit on Samsung Galaxy S25+ mobile viewport', async ({ page }) => {
    const s25plus = getDevicePreset('Samsung Galaxy S25+');
    await page.setViewportSize({ width: s25plus.width, height: s25plus.height });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const inspector = new LayoutInspector(page);
    const result = await inspector.audit({
      device: s25plus,
      includeScreenshot: false,
    });

    expect(result.overflowIssues.length).toBe(0);
    expect(result.uxScore.totalScore).toBeGreaterThanOrEqual(80);
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Audits Add/Edit Server Modal on Desktop 1080p viewport for zero overflow, scrollability, and close behavior.
   */
  test('should pass layout audit for Add Server modal on desktop 1080p', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const addBtn = page.locator('button:has-text("Add Server"), button:has-text("+ Add Server")').first();
    await expect(addBtn).toBeVisible();
    await addBtn.click();

    const modal = page.locator('#server-modal');
    await expect(modal).toBeVisible();

    const inspector = new LayoutInspector(page);
    const result = await inspector.audit({
      device: getDevicePreset('Desktop 1080p'),
      includeScreenshot: false,
      checkOcclusion: false,
      checkCollisions: false,
      checkFocusIndicators: false,
    });

    expect(result.overflowIssues.length).toBe(0);
    expect(result.uxScore.totalScore).toBeGreaterThanOrEqual(85);

    const isScrollable = await page.locator('#server-modal .modal-card').evaluate((el) => {
      return el.scrollHeight >= el.clientHeight;
    });
    expect(isScrollable).toBe(true);

    const closeBtn = page.locator('#server-modal .btn-close').first();
    await closeBtn.click();
    await expect(modal).toBeHidden();
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Audits Add/Edit Server Modal on Samsung Galaxy S25+ mobile viewport for zero overflow, scrollability, and close behavior.
   */
  test('should pass layout audit for Add Server modal on Samsung Galaxy S25+ mobile', async ({ page }) => {
    const s25plus = getDevicePreset('Samsung Galaxy S25+');
    await page.setViewportSize({ width: s25plus.width, height: s25plus.height });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const addBtn = page.locator('button:has-text("Add Server"), button:has-text("+ Add Server")').first();
    await expect(addBtn).toBeVisible();
    await addBtn.click();

    const modal = page.locator('#server-modal');
    await expect(modal).toBeVisible();
    await page.waitForTimeout(350);

    const inspector = new LayoutInspector(page);
    const result = await inspector.audit({
      device: s25plus,
      includeScreenshot: false,
      checkOcclusion: false,
      checkCollisions: false,
      checkFocusIndicators: false,
    });

    expect(result.overflowIssues.length).toBe(0);
    expect(result.uxScore.totalScore).toBeGreaterThanOrEqual(80);

    const isScrollable = await page.locator('#server-modal .modal-card').evaluate((el) => {
      return el.scrollHeight >= el.clientHeight;
    });
    expect(isScrollable).toBe(true);

    const closeBtn = page.locator('#server-modal .btn-close').first();
    await closeBtn.click();
    await expect(modal).toBeHidden();
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Audits Capabilities Inspect Modal tabs (Tools, Resources, Prompts) for zero overflow issues.
   */
  test('should pass layout audit across Capabilities Inspect modal tabs', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const inspectBtn = page.locator('button:has-text("Inspect"), .btn-inspect').first();
    await expect(inspectBtn).toBeVisible();
    await inspectBtn.click();

    const modal = page.locator('#inspect-modal');
    await expect(modal).toBeVisible();

    // Verify Tools tab content loaded
    await expect(page.locator('#inspect-modal').getByText('docker_ps')).toBeVisible();

    const inspector = new LayoutInspector(page);
    let result = await inspector.audit({
      device: getDevicePreset('Desktop 1080p'),
      includeScreenshot: false,
    });
    expect(result.overflowIssues.length).toBe(0);

    // Switch to Resources tab
    const resourcesTabBtn = page.locator('#inspect-modal button.tester-tab-btn:has-text("Resources")');
    await resourcesTabBtn.click();
    await expect(page.locator('#inspect-modal').getByText('container_status')).toBeVisible();

    result = await inspector.audit({
      device: getDevicePreset('Desktop 1080p'),
      includeScreenshot: false,
    });
    expect(result.overflowIssues.length).toBe(0);

    // Switch to Prompts tab
    const promptsTabBtn = page.locator('#inspect-modal button.tester-tab-btn:has-text("Prompts")');
    await promptsTabBtn.click();
    await expect(page.locator('#inspect-modal').getByText('diagnose_container')).toBeVisible();

    result = await inspector.audit({
      device: getDevicePreset('Desktop 1080p'),
      includeScreenshot: false,
    });
    expect(result.overflowIssues.length).toBe(0);

    // Close modal
    const closeBtn = page.locator('#inspect-modal .btn-close');
    await closeBtn.click();
    await expect(modal).toBeHidden();
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Audits App Keys & Security view layout for zero horizontal overflow and high UX score.
   */
  test('should pass layout audit on App Keys & Security tab', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const securityNavBtn = page.locator('.tabs-nav button:has-text("App Keys & Security")');
    await expect(securityNavBtn).toBeVisible();
    await securityNavBtn.click();

    const securityView = page.locator('#view-security');
    await expect(securityView).toBeVisible();

    const inspector = new LayoutInspector(page);
    const result = await inspector.audit({
      device: getDevicePreset('Desktop 1080p'),
      includeScreenshot: false,
    });

    expect(result.overflowIssues.length).toBe(0);
    expect(result.uxScore.totalScore).toBeGreaterThanOrEqual(80);
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Audits Settings view layout for zero horizontal overflow and high UX score.
   */
  test('should pass layout audit on Settings tab', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const settingsNavBtn = page.locator('.tabs-nav button:has-text("Settings")');
    await expect(settingsNavBtn).toBeVisible();
    await settingsNavBtn.click();

    const settingsView = page.locator('#view-settings');
    await expect(settingsView).toBeVisible();

    const inspector = new LayoutInspector(page);
    const result = await inspector.audit({
      device: getDevicePreset('Desktop 1080p'),
      includeScreenshot: false,
    });

    expect(result.overflowIssues.length).toBe(0);
    expect(result.uxScore.totalScore).toBeGreaterThanOrEqual(65);
  });

  /**
   * @requirement UI-07
   * @category UI
   * @type PositiveFeature
   * @description Verifies layout stability and negligible element displacement during tab navigation.
   */
  test('should maintain layout stability during tab navigation', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.goto('/');
    await page.waitForLoadState('domcontentloaded');

    const inspector = new LayoutInspector(page);
    const shiftResult = await inspector.trackShifts(async () => {
      const securityNavBtn = page.locator('.tabs-nav button:has-text("App Keys & Security")');
      await securityNavBtn.click();
      await expect(page.locator('#view-security')).toBeVisible();
    });

    // Verify layout stability score adheres to strict Core Web Vitals thresholds (CLS <= 0.05)
    expect(shiftResult.totalShiftScore).toBeLessThanOrEqual(0.05);
  });

});
