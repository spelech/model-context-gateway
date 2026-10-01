import { Page, Locator } from '@playwright/test';

export class TestBenchPage {
  readonly page: Page;
  readonly serverSelect: Locator;
  readonly toolSelect: Locator;
  readonly executeBtn: Locator;
  readonly outputConsole: Locator;
  readonly semanticQueryInput: Locator;
  readonly searchToolsBtn: Locator;
  readonly promptTabBtn: Locator;
  readonly resourceTabBtn: Locator;
  readonly promptServerSelect: Locator;
  readonly promptNameSelect: Locator;
  readonly promptExecuteBtn: Locator;
  readonly resourceServerSelect: Locator;
  readonly resourceNameSelect: Locator;
  readonly resourceUriInput: Locator;
  readonly resourceReadBtn: Locator;

  constructor(page: Page) {
    this.page = page;
    this.serverSelect = page.locator('[data-testid="tool-server-select"], select#tester-server');
    this.toolSelect = page.locator('[data-testid="tool-name-select"], select#tester-tool');
    this.executeBtn = page.locator('[data-testid="tool-execute-btn"]');
    this.outputConsole = page.locator('[data-testid="console-response-output"], #jsonrpc-response');
    this.semanticQueryInput = page.locator('[data-testid="semantic-query-input"], #semantic-search-query');
    this.searchToolsBtn = page.locator('[data-testid="semantic-search-btn"]');

    this.toolTabBtn = page.locator('[data-testid="testbench-tab-tools"], button:has-text("Tools")');
    this.promptTabBtn = page.locator('[data-testid="testbench-tab-prompts"]');
    this.resourceTabBtn = page.locator('[data-testid="testbench-tab-resources"]');
    this.semanticTabBtn = page.locator('[data-testid="testbench-tab-semantic"], button:has-text("Semantic Router")');
    this.promptServerSelect = page.locator('[data-testid="prompt-server-select"]');
    this.promptNameSelect = page.locator('[data-testid="prompt-name-select"]');
    this.promptExecuteBtn = page.locator('[data-testid="prompt-execute-btn"]');
    this.resourceServerSelect = page.locator('[data-testid="resource-server-select"]');
    this.resourceNameSelect = page.locator('[data-testid="resource-name-select"]');
    this.resourceUriInput = page.locator('[data-testid="resource-uri-input"]');
    this.resourceReadBtn = page.locator('[data-testid="resource-read-btn"]');
  }

  async selectServerAndTool(serverId: string, toolName?: string) {
    if (await this.toolTabBtn.isVisible() && !(await this.serverSelect.isVisible())) {
      await this.toolTabBtn.click();
    }
    await this.serverSelect.waitFor({ state: 'visible' });
    await this.serverSelect.selectOption(serverId);
    if (toolName) {
      await this.toolSelect.waitFor({ state: 'visible' });
      await this.toolSelect.selectOption(toolName);
    }
  }

  async executeTool() {
    await this.executeBtn.click();
  }

  async searchTools(query: string) {
    if (await this.semanticTabBtn.isVisible()) {
      await this.semanticTabBtn.click();
    }
    await this.semanticQueryInput.waitFor({ state: 'visible' });
    await this.semanticQueryInput.fill(query);
    await this.searchToolsBtn.click();
  }
}
