import { chromium } from '../frontend/node_modules/playwright/index.mjs';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outputDir = path.resolve(__dirname, '../docs/assets');

if (!fs.existsSync(outputDir)) {
  fs.mkdirSync(outputDir, { recursive: true });
}

async function capture() {
  console.log('Launching Chromium to capture documentation screenshots...');
  const browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    extraHTTPHeaders: {
      'Remote-User': 'steve',
      'Remote-Groups': 'full_admin,house_member',
      'Remote-Name': 'Steve Pelech',
      'Remote-User-Sid': 'S-1-5-32-544'
    }
  });

  const page = await context.newPage();
  const baseUrl = process.env.MCG_URL || process.env.GATEWAY_URL || 'http://localhost:8088';

  // 1. Dashboard Overview
  console.log('Capturing Dashboard Overview...');
  await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);
  await page.screenshot({ path: path.join(outputDir, 'dashboard.jpg'), quality: 90, type: 'jpeg' });

  // 2. Add Server Modal
  console.log('Capturing Add Server Modal...');
  const addServerBtn = page.getByRole('button', { name: /Add Server/i });
  if (await addServerBtn.isVisible()) {
    await addServerBtn.click();
    await page.waitForSelector('#server-modal, .modal-card', { state: 'visible' });
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(outputDir, 'add_server_modal.jpg'), quality: 90, type: 'jpeg' });
    const closeBtn = page.locator('#server-modal .btn-close, .modal-backdrop .btn-close');
    if (await closeBtn.isVisible()) await closeBtn.click();
    await page.waitForTimeout(500);
  }

  // 3. Server Inspect Modal
  console.log('Capturing Server Inspect Modal...');
  const inspectBtn = page.locator('.btn-inspect').first();
  if (await inspectBtn.isVisible()) {
    await inspectBtn.click();
    await page.waitForSelector('.modal-card', { state: 'visible' });
    await page.waitForTimeout(800);
    await page.screenshot({ path: path.join(outputDir, 'server_inspect_modal.jpg'), quality: 90, type: 'jpeg' });
    const closeInspectBtn = page.locator('.modal-backdrop .btn-close');
    if (await closeInspectBtn.isVisible()) await closeInspectBtn.click();
    await page.waitForTimeout(500);
  }

  // 4. My MCP Servers (User Provided Auth)
  console.log('Capturing My MCP Servers view...');
  const myServersTab = page.getByRole('button', { name: /My MCP Servers/i });
  if (await myServersTab.isVisible()) {
    await myServersTab.click();
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(outputDir, 'my_mcp_servers_view.jpg'), quality: 90, type: 'jpeg' });
  }

  // 5. App Keys & Clients (Security View)
  console.log('Capturing App Keys & Security View...');
  const clientsTab = page.getByRole('button', { name: /Clients|App Keys/i });
  if (await clientsTab.isVisible()) {
    await clientsTab.click();
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(outputDir, 'security_view.jpg'), quality: 90, type: 'jpeg' });

    // 6. Create App Key Modal
    const createAppKeyBtn = page.getByRole('button', { name: /Create App Key|Add Key/i }).first();
    if (await createAppKeyBtn.isVisible()) {
      await createAppKeyBtn.click();
      await page.waitForSelector('#add-appkey-modal, .modal-card', { state: 'visible' });
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'add_appkey_modal.jpg'), quality: 90, type: 'jpeg' });
      const closeKeyModal = page.locator('#add-appkey-modal .btn-close, .modal-backdrop .btn-close');
      if (await closeKeyModal.isVisible()) await closeKeyModal.click();
      await page.waitForTimeout(500);
    }

    // 7. Register Client Modal
    const registerClientBtn = page.getByRole('button', { name: /Register Client|Add Client/i }).first();
    if (await registerClientBtn.isVisible()) {
      await registerClientBtn.click();
      await page.waitForSelector('#add-client-modal, .modal-card', { state: 'visible' });
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'registered_client_modal.jpg'), quality: 90, type: 'jpeg' });
      const closeClientModal = page.locator('#add-client-modal .btn-close, .modal-backdrop .btn-close');
      if (await closeClientModal.isVisible()) await closeClientModal.click();
      await page.waitForTimeout(500);
    }
  }

  // 8. Test Bench View
  console.log('Capturing Test Bench View...');
  const testbenchTab = page.getByRole('button', { name: /Test Bench/i });
  if (await testbenchTab.isVisible()) {
    await testbenchTab.click();
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(outputDir, 'test_bench_view.jpg'), quality: 90, type: 'jpeg' });

    // 8b. Semantic Router Simulator
    console.log('Capturing Semantic Router Simulator...');
    const semanticTabBtn = page.locator('[data-testid="testbench-tab-semantic"], button:has-text("Semantic Router")').first();
    if (await semanticTabBtn.isVisible()) {
      await semanticTabBtn.click();
      await page.waitForTimeout(1000);
      const queryInput = page.locator('[data-testid="semantic-query-input"]');
      if (await queryInput.isVisible()) {
        await queryInput.fill('search media in plex');
        const searchBtn = page.locator('[data-testid="semantic-search-btn"]');
        if (await searchBtn.isVisible()) {
          await searchBtn.click();
          await page.waitForTimeout(1500);
        }
      }
      await page.screenshot({ path: path.join(outputDir, 'semantic_router_simulator.jpg'), quality: 90, type: 'jpeg' });
    }
  }

  // 9. Settings View & Tabs
  console.log('Capturing Settings Sub-Tabs...');
  const settingsTab = page.getByRole('button', { name: /Settings/i });
  if (await settingsTab.isVisible()) {
    await settingsTab.click();
    await page.waitForTimeout(1000);

    // Vector & Search
    const vectorSubTab = page.locator('button:has-text("Vector & Search"), .nav-tab:has-text("Vector")').first();
    if (await vectorSubTab.isVisible()) {
      await vectorSubTab.click();
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'settings_vector_search.jpg'), quality: 90, type: 'jpeg' });
    }

    // Identity & Auth
    const identitySubTab = page.locator('button:has-text("Identity & Auth"), .nav-tab:has-text("Identity")').first();
    if (await identitySubTab.isVisible()) {
      await identitySubTab.click();
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'settings_identity_auth.jpg'), quality: 90, type: 'jpeg' });
    }

    // Secret Providers
    const secretsSubTab = page.locator('button:has-text("Secret Providers"), .nav-tab:has-text("Secret Providers")').first();
    if (await secretsSubTab.isVisible()) {
      await secretsSubTab.click();
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'settings_secret_providers.jpg'), quality: 90, type: 'jpeg' });
    }

    // Prompts & Resources / Custom Files
    const promptsSubTab = page.locator('button:has-text("Prompts & Resources"), .nav-tab:has-text("Prompts")').first();
    if (await promptsSubTab.isVisible()) {
      await promptsSubTab.click();
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'settings_prompts_resources.jpg'), quality: 90, type: 'jpeg' });
    }

    // Access Control
    const accessSubTab = page.locator('button:has-text("Access Control"), .nav-tab:has-text("Access Control")').first();
    if (await accessSubTab.isVisible()) {
      await accessSubTab.click();
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'settings_access_control.jpg'), quality: 90, type: 'jpeg' });
    }

    // General / Backups
    const backupsSubTab = page.locator('button:has-text("Backups"), .nav-tab:has-text("Backups")').first();
    if (await backupsSubTab.isVisible()) {
      await backupsSubTab.click();
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(outputDir, 'settings_backups.jpg'), quality: 90, type: 'jpeg' });
    }

    // Overall Settings View
    await page.screenshot({ path: path.join(outputDir, 'settings_view.jpg'), quality: 90, type: 'jpeg' });
  }

  // 10. OAuth Consent Screen
  console.log('Capturing OAuth Consent Screen...');
  await page.goto(`${baseUrl}/consent?client_id=slack-enterprise-app&client_name=Slack%20Enterprise%20Integration`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  await page.screenshot({ path: path.join(outputDir, 'oauth_consent_screen.jpg'), quality: 90, type: 'jpeg' });

  console.log('Successfully captured all live documentation screenshots into docs/assets/!');
  await browser.close();
}

capture().catch((err) => {
  console.error('Error capturing screenshots:', err);
  process.exit(1);
});
