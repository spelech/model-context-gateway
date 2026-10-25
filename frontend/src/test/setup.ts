import '@testing-library/jest-dom';
import { beforeEach, vi } from 'vitest';
import { useUserStore } from '../stores/useUserStore';
import { useServerStore } from '../stores/useServerStore';
import { useClientStore } from '../stores/useClientStore';
import { useAppKeyStore } from '../stores/useAppKeyStore';
import { useSettingsStore } from '../stores/useSettingsStore';
import { useToastStore } from '../stores/useToastStore';
import { useConfirmStore } from '../stores/useConfirmStore';
import { useLogStore } from '../stores/useLogStore';
import { useConfigStore } from '../stores/useConfigStore';

export interface MockFetchCall {
  url: string;
  options?: RequestInit;
}

export type MockHandler = (url: string, options?: RequestInit) => any;

// Internal state for fetch mock
let fetchCalls: MockFetchCall[] = [];
const routeHandlers: Map<string | RegExp, MockHandler> = new Map();

export const defaultMockData = {
  me: {
    authenticated: true,
    username: 'admin',
    name: 'Admin User',
    email: 'admin@example.com',
    groups: ['full_admin', 'engineering']
  },
  health: {
    status: 'healthy',
    version: '4.5.6'
  },
  servers: [
    {
      id: 'notes-rag',
      displayName: 'Notes RAG',
      url: 'http://notes-rag-mcp:3000/sse',
      enabled: true,
      hidden: false,
      type: 'sse',
      categories: ['infrastructure', 'notes'],
      secretProvider: 'None',
      secretItemKey: '',
      authShape: 'bearer',
      customHeaderName: '',
      hasApiKey: false,
      connectionStatus: 'Connected',
      connectionAttempts: 0,
      connectionError: ''
    }
  ],
  clients: [
    {
      id: 'client-1',
      clientId: 'cursor-ide',
      displayName: 'Cursor IDE',
      isDynamic: false,
      scopes: ['mcp_client']
    }
  ],
  appKeys: [
    {
      id: 'key-1',
      name: 'OpenClaw Agent',
      username: 'admin',
      keyPrefix: 'mcp_live_a1b2',
      scopes: ['all'],
      createdAt: '2026-08-14T00:00:00Z'
    }
  ],
  appKeyLimits: {
    globalMax: 50,
    userMax: 10,
    totalActiveKeys: 1,
    userActiveKeys: 1,
    isLimitReached: false
  },
  userQuotas: [
    {
      username: 'admin',
      maxKeys: 10,
      createdAt: '2026-08-14T00:00:00Z',
      updatedAt: '2026-08-14T00:00:00Z'
    }
  ],
  settings: {
    embeddingProvider: 'local',
    embeddingModelDir: 'data/models',
    embeddingApiUrl: '',
    embeddingApiModel: 'all-MiniLM-L6-v2',
    embeddingApiKey: '',
    requireManualApproval: false,
    enableCodeMode: true,
    codeModeExecutionTimeoutSeconds: 30,
    codeModeMaxToolCalls: 50,
    codeModeMaxMemoryMb: 64,
    codeModeMaxStatements: 100000,
    codeModeAllowedGroups: '',
  },
  authProviders: [
    {
      providerName: 'ActiveDirectory',
      displayName: 'Active Directory',
      isEnabled: false
    },
    {
      providerName: 'HeaderAuth',
      displayName: 'OIDC / Reverse Proxy Headers',
      isEnabled: true,
      userHeader: 'Remote-User',
      groupsHeader: 'Remote-Groups'
    }
  ],
  secretProviders: [
    {
      providerName: 'Vault',
      displayName: 'HashiCorp Vault (KV v2)',
      isEnabled: false,
      configJson: JSON.stringify({
        address: 'http://vault:8200',
        token: 's.mockVaultToken',
        mountPath: 'secret/data/'
      })
    },
    {
      providerName: 'WindowsRegistry',
      displayName: 'Windows Registry (DPAPI)',
      isEnabled: false,
      configJson: JSON.stringify({
        keyPath: 'HKCU\\Software\\McpRouter\\Secrets'
      })
    },
    {
      providerName: 'Environment',
      displayName: 'Container Environment',
      isEnabled: true,
      configJson: JSON.stringify({
        prefix: 'MCP_SECRET_'
      })
    }
  ],
  customFiles: [
    {
      type: 'prompts' as const,
      name: 'system-assistant.json',
      sizeBytes: 1024,
      lastModified: '2026-08-14T00:00:00Z'
    }
  ],
  policies: [
    {
      id: 'pol-1',
      targetId: 'server:ha',
      requiredGroup: 'house_member',
      isAllowed: true
    }
  ],
  mappings: [
    {
      id: 'map-1',
      externalId: 'S-1-5-21-1234567890-1234567890-1234567890-500',
      internalGroup: 'Administrators'
    }
  ],
  approvals: []
};

export type MockResponseBody = MockHandler | Record<string, any> | any[] | string | number | boolean | null | undefined;

/**
 * Register a mock handler or static value for a specific URL endpoint.
 */
export function mockApiResponse(
  urlOrPattern: string | RegExp,
  response: MockResponseBody,
  status: number = 200,
  statusText: string = 'OK'
): void {
  routeHandlers.set(urlOrPattern, (url: string, options?: RequestInit) => {
    const data = typeof response === 'function' ? (response as MockHandler)(url, options) : response;
    return { data, status, statusText };
  });
}

/**
 * Set multiple mock routes at once.
 */
export function setMockRoutes(routes: Record<string, any>) {
  for (const [pattern, res] of Object.entries(routes)) {
    mockApiResponse(pattern, res);
  }
}

/**
 * Get all captured fetch calls.
 */
export function getFetchCalls(): MockFetchCall[] {
  return [...fetchCalls];
}

/**
 * Clear captured calls and custom route handlers.
 */
export function resetFetchMocks() {
  fetchCalls = [];
  routeHandlers.clear();
  setupDefaultRoutes();
}

function setupDefaultRoutes() {
  mockApiResponse('/api/me', defaultMockData.me);
  mockApiResponse('/health', defaultMockData.health);
  mockApiResponse('/api/servers/reconnect-all', { success: true });
  mockApiResponse(/\/api\/servers\/[^/]+\/reconnect/, { success: true });
  mockApiResponse(/\/api\/servers\/[^/]+\/inspect/, { tools: [], resources: [], prompts: [] });
  mockApiResponse('/api/servers', (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      const body = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
      return { id: body?.id || 'new-server-id', ...body };
    }
    return defaultMockData.servers;
  });
  mockApiResponse(/\/api\/servers\/[^/]+/, (_url: string, options?: RequestInit) => {
    if (options?.method === 'PUT') {
      const body = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
      return { success: true, ...body };
    }
    if (options?.method === 'DELETE') {
      return { success: true };
    }
    return defaultMockData.servers[0];
  });
  mockApiResponse('/api/clients', (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      return { clientId: 'new-client-id-123', clientSecret: 'mcp_secret_xyz789' };
    }
    return defaultMockData.clients;
  });
  mockApiResponse(/\/api\/clients\/[^/]+/, (_url: string, options?: RequestInit) => {
    if (options?.method === 'DELETE') {
      return { success: true };
    }
    return { success: true };
  });
  mockApiResponse('/api/appkeys/limits', defaultMockData.appKeyLimits);
  mockApiResponse('/api/appkeys', (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      const body = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
      return {
        id: 'new-key-id-999',
        name: body?.name || 'New App Key',
        username: body?.username || 'admin',
        keyPrefix: 'mcp_live_9999',
        plaintextKey: 'mcp_live_9999_secret_plaintext_token_example',
        scopes: body?.scopes || ['all'],
        expiresAt: body?.expiresInDays ? new Date(Date.now() + body.expiresInDays * 86400000).toISOString() : undefined,
        createdAt: new Date().toISOString()
      };
    }
    return defaultMockData.appKeys;
  });
  mockApiResponse(/\/api\/appkeys\/[^/]+/, (_url: string, options?: RequestInit) => {
    if (options?.method === 'DELETE') {
      return { success: true };
    }
    return { success: true };
  });
  mockApiResponse('/api/appkeys/quotas', (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      const body = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
      return { success: true, username: body?.username || 'user', maxKeys: body?.maxKeys ?? 5 };
    }
    return defaultMockData.userQuotas;
  });
  mockApiResponse(/\/api\/appkeys\/quotas\/[^/]+/, (_url: string, options?: RequestInit) => {
    if (options?.method === 'DELETE') {
      return { success: true };
    }
    return { success: true };
  });
  mockApiResponse('/api/settings', (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      return { success: true };
    }
    return defaultMockData.settings;
  });
  mockApiResponse('/api/providers/auth', defaultMockData.authProviders);
  mockApiResponse('/api/providers/secrets', defaultMockData.secretProviders);
  mockApiResponse('/api/approvals', defaultMockData.approvals);
  mockApiResponse(/\/api\/approvals\/[^/]+\/action/, { success: true });
  mockApiResponse('/api/custom-files', defaultMockData.customFiles);
  mockApiResponse(/\/api\/custom-files\/[^/]+\/[^/]+/, (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST' || options?.method === 'DELETE') {
      return { success: true };
    }
    return { content: '{\n  "name": "mock-custom-content"\n}' };
  });
  mockApiResponse('/api/test/tools', []);
  mockApiResponse('/api/test/prompts', []);
  mockApiResponse('/api/test/resources', []);
  mockApiResponse('/api/permissions/policies', (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      const body = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
      return { id: body?.id || 'new-pol-id', ...body };
    }
    return defaultMockData.policies;
  });
  mockApiResponse(/\/api\/permissions\/policies\/[^/]+/, (_url: string, options?: RequestInit) => {
    if (options?.method === 'DELETE') {
      return { success: true };
    }
    return { success: true };
  });
  mockApiResponse('/api/permissions/mappings', (_url: string, options?: RequestInit) => {
    if (options?.method === 'POST') {
      const body = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
      return { id: body?.id || 'new-map-id', ...body };
    }
    return defaultMockData.mappings;
  });
  mockApiResponse(/\/api\/permissions\/mappings\/[^/]+/, (_url: string, options?: RequestInit) => {
    if (options?.method === 'DELETE') {
      return { success: true };
    }
    return { success: true };
  });
}

/**
 * Reset all Zustand stores to their initial states.
 */
export function resetAllStores() {
  useUserStore.setState({
    user: null,
    version: '5.2.0',
    service: 'ModelContextGateway',
    isLoadingUser: false
  });

  useServerStore.setState({
    servers: [],
    isLoadingServers: false,
    searchQuery: '',
    sortBy: 'status-priority',
    groupBy: 'none',
    currentPage: 1,
    pageSize: 6,
    collapsedGroups: [],
    isAddEditOpen: false,
    editingServer: null,
    isInspectOpen: false,
    inspectServer: null,
    inspectData: { tools: [], resources: [], prompts: [] },
    inspectLoading: false,
    inspectActiveTab: 'tools',
    inspectSearchQuery: ''
  });

  useClientStore.setState({
    clients: [],
    isLoadingClients: false,
    isAddClientOpen: false,
    createdClientResult: null
  });

  useAppKeyStore.setState({
    appKeys: [],
    limits: null,
    keyTypeTab: 'personal',
    userQuotas: [],
    isLoading: false,
    isLoadingQuotas: false,
    isCreateModalOpen: false,
    createdResult: null
  });

  useSettingsStore.setState({
    embeddingSettings: null,
    authProviders: [],
    secretProviders: [],
    customFiles: [],
    policies: [],
    mappings: [],
    isLoadingSettings: false,
    isSavingSettings: false,
    isCustomFileOpen: false,
    editingFileMeta: null,
    editingFileContent: '',
    activeFileModalTab: 'editor',
    isPolicyModalOpen: false,
    editingPolicy: null,
    isMappingModalOpen: false,
    editingMapping: null
  });

  useToastStore.setState({
    toasts: []
  });

  useConfirmStore.setState({
    isOpen: false,
    options: { message: '' },
    resolve: null
  });

  useLogStore.setState({
    logs: [],
    typeFilter: 'system',
    levelFilter: 'ALL',
    autoScroll: true,
    isLoadingLogs: false
  });

  useConfigStore.setState({
    branding: null
  });
}

// Install mock fetch globally
globalThis.fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
  const url = typeof input === 'string' ? input : input.toString();
  fetchCalls.push({ url, options: init });

  const cleanUrl = url.split('?')[0];
  const handlers = Array.from(routeHandlers.entries()).reverse();

  // First pass: exact string matching (most specific)
  for (const [pattern, handler] of handlers) {
    if (typeof pattern === 'string' && (url === pattern || cleanUrl === pattern)) {
      const result = handler(url, init);
      const { data, status = 200, statusText = 'OK' } = result || {};
      const ok = status >= 200 && status < 300;

      return {
        ok,
        status,
        statusText,
        json: async () => data,
        text: async () => (typeof data === 'string' ? data : JSON.stringify(data)),
        headers: new Headers({ 'Content-Type': 'application/json' })
      } as unknown as Response;
    }
  }

  // Second pass: regex pattern matching
  for (const [pattern, handler] of handlers) {
    if (pattern instanceof RegExp && (pattern.test(url) || pattern.test(cleanUrl))) {
      const result = handler(url, init);
      const { data, status = 200, statusText = 'OK' } = result || {};
      const ok = status >= 200 && status < 300;

      return {
        ok,
        status,
        statusText,
        json: async () => data,
        text: async () => (typeof data === 'string' ? data : JSON.stringify(data)),
        headers: new Headers({ 'Content-Type': 'application/json' })
      } as unknown as Response;
    }
  }

  // Fallback 404 response
  return {
    ok: false,
    status: 404,
    statusText: 'Not Found',
    json: async () => ({ error: 'Route not mocked in test harness', url }),
    text: async () => `Not Found: ${url}`,
    headers: new Headers({ 'Content-Type': 'application/json' })
  } as unknown as Response;
});

// Setup mock window & browser environment helpers
if (typeof window !== 'undefined') {
  window.confirm = vi.fn(() => true);
  window.alert = vi.fn();
  Object.defineProperty(navigator, 'clipboard', {
    value: {
      writeText: vi.fn().mockResolvedValue(undefined)
    },
    writable: true,
    configurable: true
  });
}

// Global deterministic test setup
beforeEach(() => {
  resetAllStores();
  resetFetchMocks();
  if (typeof localStorage !== 'undefined') {
    localStorage.clear();
  }
  if (typeof window !== 'undefined') {
    window.confirm = vi.fn(() => true);
    window.alert = vi.fn();
    (navigator.clipboard.writeText as any)?.mockClear?.();
  }
});
