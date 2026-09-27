import { Page, Locator, expect } from '@playwright/test';

export class ServerModalPage {
  readonly page: Page;
  readonly modal: Locator;
  readonly serverIdInput: Locator;
  readonly serverNameInput: Locator;
  readonly transportTypeSelect: Locator;
  readonly serverUrlInput: Locator;
  readonly categoryInput: Locator;
  readonly secretProviderSelect: Locator;
  readonly secretMountInput: Locator;
  readonly secretPathInput: Locator;
  readonly secretFieldInput: Locator;
  readonly secretKeyInput: Locator;
  readonly saveBtn: Locator;
  readonly cancelBtn: Locator;

  constructor(page: Page) {
    this.page = page;
    this.modal = page.locator('[data-testid="server-modal"], #server-modal').first();
    this.serverIdInput = page.locator('input#server-id, input[name="id"]');
    this.serverNameInput = page.locator('[data-testid="server-name-input"], #server-name');
    this.transportTypeSelect = page.locator('[data-testid="server-type-select"], #server-type');
    this.serverUrlInput = page.locator('[data-testid="server-url-input"], #server-url');
    this.categoryInput = page.locator('[data-testid="server-category-input"], #server-category');
    this.secretProviderSelect = page.locator('[data-testid="server-secret-provider-select"], #server-secret-provider');
    this.secretMountInput = page.locator('input#secret-mount, input[name="secretMount"]');
    this.secretPathInput = page.locator('input#secret-path, input[name="secretPath"]');
    this.secretFieldInput = page.locator('input#secret-field, input[name="secretField"]');
    this.secretKeyInput = page.locator('[data-testid="server-secret-key-input"], #server-secret-key');
    this.saveBtn = page.locator('[data-testid="server-save-btn"], #btn-save').first();
    this.cancelBtn = page.locator('[data-testid="server-cancel-btn"], [data-testid="server-close-btn"]').first();
  }

  async fillServerForm(details: {
    id?: string;
    name: string;
    type?: 'http' | 'sse' | 'stdio' | 'streamable';
    url: string;
    secretProvider?: 'None' | 'Environment' | 'Vault' | 'WindowsRegistry' | 'UserProvided';
    vaultMount?: string;
    vaultPath?: string;
    vaultField?: string;
    secretKey?: string;
    category?: string;
  }) {
    await this.modal.waitFor({ state: 'visible', timeout: 10000 });

    if (details.name) {
      await this.serverNameInput.fill(details.name);
    }
    if (details.type) {
      await this.transportTypeSelect.selectOption(details.type);
    }
    if (details.url) {
      await this.serverUrlInput.fill(details.url);
    }
    if (details.category) {
      await this.categoryInput.fill(details.category);
    }

    if (details.secretProvider) {
      await this.secretProviderSelect.selectOption(details.secretProvider);

      if (details.secretProvider === 'Vault') {
        const vaultKey = `${details.vaultMount || ''}:${details.vaultPath || ''}:${details.vaultField || ''}`;
        await this.secretKeyInput.fill(vaultKey);
      } else if (details.secretKey) {
        await this.secretKeyInput.fill(details.secretKey);
      }
    }
  }

  async save() {
    await this.saveBtn.click();
    await expect(this.modal).toBeHidden({ timeout: 15000 });
  }
}
