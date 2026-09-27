import { Page, Locator } from '@playwright/test';

export class DashboardPage {
  readonly page: Page;
  readonly navDashboardBtn: Locator;
  readonly navTestbenchBtn: Locator;
  readonly navSettingsBtn: Locator;
  readonly navClientsBtn: Locator;
  readonly addServerBtn: Locator;
  readonly searchInput: Locator;
  readonly sortBySelect: Locator;
  readonly groupBySelect: Locator;

  constructor(page: Page) {
    this.page = page;
    this.navDashboardBtn = page.locator('[data-testid="tab-dashboard"], button:has-text("Overview")');
    this.navTestbenchBtn = page.locator('[data-testid="tab-testbench"], button:has-text("Test Bench")');
    this.navSettingsBtn = page.locator('[data-testid="tab-settings"], button:has-text("Settings")');
    this.navClientsBtn = page.locator('[data-testid="tab-security"], button:has-text("Clients"), button:has-text("Security")');
    this.addServerBtn = page.locator('[data-testid="btn-add-server"], #btn-add-server');
    this.searchInput = page.locator('[data-testid="server-search-input"], #server-search');
    this.sortBySelect = page.locator('[data-testid="server-sort-select"], #server-sort-by');
    this.groupBySelect = page.locator('[data-testid="server-group-select"], #server-group-by');
  }

  async goto() {
    await this.page.goto('/');
  }

  async navigateToTestbench() {
    await this.navTestbenchBtn.click();
  }

  async navigateToSettings() {
    await this.navSettingsBtn.click();
  }

  async navigateToClients() {
    await this.navClientsBtn.click();
  }

  async searchServer(query: string) {
    await this.searchInput.fill(query);
  }

  getServerCard(serverId: string): Locator {
    return this.page.locator(`[data-testid="server-card-${serverId}"], [data-server-id="${serverId}"], .server-item:has-text("${serverId}")`);
  }

  getServerStatusBadge(serverId: string): Locator {
    return this.page.locator(`[data-testid="server-status-${serverId}"], [data-server-id="${serverId}"] .indicator, [data-server-id="${serverId}"] .server-badge`);
  }

  getServerStatusBadgeByName(name: string): Locator {
    return this.page.locator(`.server-item:has-text("${name}"), .server-card:has-text("${name}")`).locator('.server-badge, .indicator, .status-badge').first();
  }

  async getServerIdByName(name: string): Promise<string> {
    const card = this.page.locator(`.server-item:has-text("${name}"), .server-card:has-text("${name}")`).first();
    await card.waitFor({ state: 'visible' });
    const id = await card.getAttribute('data-server-id');
    return id || '';
  }
}
