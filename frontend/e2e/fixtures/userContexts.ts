/* eslint-disable react-hooks/rules-of-hooks */
import { test as base, expect, Page } from '@playwright/test';

/**
 * Caller contexts for multi-user pairwise E2E matrix testing.
 */
export const USER_CONTEXTS = {
  admin: {
    'Remote-User': 'admin_user',
    'Remote-Groups': 'full_admin,devops',
    'Remote-Name': 'Admin User',
    'Remote-User-Sid': 'S-1-5-32-544'
  },
  operator: {
    'Remote-User': 'operator_user',
    'Remote-Groups': 'SmartHomeOperators',
    'Remote-Name': 'SmartHome Operator',
    'Remote-User-Sid': 'S-1-5-21-1002'
  },
  guest: {
    'Remote-User': 'guest_user',
    'Remote-Groups': 'Guests',
    'Remote-Name': 'Guest User',
    'Remote-User-Sid': 'S-1-5-21-9999'
  },
  appKey: {
    'X-App-Key': 'mcp_live_e2e_matrix_test_key'
  }
};

export type UserRole = keyof typeof USER_CONTEXTS;

export interface MultiUserFixtures {
  adminPage: Page;
  operatorPage: Page;
  guestPage: Page;
  appKeyPage: Page;
}

import { setupMockApi } from './mockApi';

export const test = base.extend<MultiUserFixtures>({
  adminPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      extraHTTPHeaders: USER_CONTEXTS.admin
    });
    const page = await context.newPage();
    await setupMockApi(page, { role: 'admin' });
    await use(page);
    await context.close();
  },
  operatorPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      extraHTTPHeaders: USER_CONTEXTS.operator
    });
    const page = await context.newPage();
    await setupMockApi(page, { role: 'operator' });
    await use(page);
    await context.close();
  },
  guestPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      extraHTTPHeaders: USER_CONTEXTS.guest
    });
    const page = await context.newPage();
    await setupMockApi(page, { role: 'guest' });
    await use(page);
    await context.close();
  },
  appKeyPage: async ({ browser }, use) => {
    const context = await browser.newContext({
      extraHTTPHeaders: USER_CONTEXTS.appKey
    });
    const page = await context.newPage();
    await setupMockApi(page, { role: 'guest' });
    await use(page);
    await context.close();
  }
});

export { expect };
