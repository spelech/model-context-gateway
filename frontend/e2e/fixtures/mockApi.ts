import { Page, Route } from '@playwright/test';

export interface MockApiOptions {
  role?: 'admin' | 'operator' | 'guest';
  servers?: any[];
  appKeys?: any[];
  clients?: any[];
  policies?: any[];
  mappings?: any[];
  customFiles?: any[];
  providers?: any[];
  secretProviders?: any[];
}

export async function setupMockApi(page: Page, options: MockApiOptions = {}) {
  const role = options.role ?? 'admin';
  const isAdmin = role === 'admin';
  const isOperator = role === 'operator';

  // In-memory mutable collections for the test session
  let servers = options.servers ?? [
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
    {
      id: 'mock-user-mcp',
      displayName: 'User MCP Server',
      type: 'sse',
      url: 'http://localhost:8023/sse',
      enabled: true,
      connectionStatus: 'Connected',
      categories: ['Personal'],
      secretProvider: 'UserProvided',
      toolsCount: 3,
      promptsCount: 1,
      resourcesCount: 0,
    },
  ];

  let appKeys = options.appKeys ?? [
    {
      id: 'key-1',
      name: 'CI/CD Token',
      username: isAdmin ? 'admin' : (isOperator ? 'operator_user' : 'guest_user'),
      keyType: 'personal',
      keyPrefix: 'mcg_live_1234',
      scopes: ['tools:read', 'tools:execute'],
      createdAt: '2026-01-01T00:00:00Z',
    },
  ];

  let clients = options.clients ?? [
    {
      id: 'client-1',
      displayName: 'Claude Desktop Integration',
      clientId: 'claude-desktop-client-123',
      clientType: 'confidential',
      isDynamic: true,
      grantTypes: ['client_credentials'],
      redirectUris: ['http://localhost:8080/callback'],
      scopes: ['tools:read'],
      createdAt: '2026-01-01T00:00:00Z',
    },
  ];

  let policies = options.policies ?? [
    {
      id: 'pol-1',
      targetId: 'server:ha',
      requiredGroup: 'SmartHomeOperators',
      isAllowed: true,
      createdAt: '2026-01-01T00:00:00Z',
    },
  ];

  let mappings = options.mappings ?? [
    {
      id: 'map-1',
      externalId: 'S-1-5-21-1001',
      internalGroup: 'Developers',
      createdAt: '2026-01-01T00:00:00Z',
    },
  ];

  let customFiles = options.customFiles ?? [
    {
      type: 'prompts',
      name: 'router__diagnose_failure.json',
      sizeBytes: 1204,
      lastModified: '2026-01-01T00:00:00Z',
      content: JSON.stringify({
        description: 'Diagnose router errors',
        arguments: [{ name: 'error_log', required: true, description: 'Log error stack' }],
        messages: [{ role: 'user', content: 'Diagnose: {{error_log}}' }],
      }),
    },
    {
      type: 'resources',
      name: 'router__system_status.md',
      sizeBytes: 2048,
      lastModified: '2026-01-01T00:00:00Z',
      content: '# System Status\n\nAll gateway subsystems are operational.',
    },
  ];

  let quotas = [
    { username: 'developer1', maxKeys: 5, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' },
    { username: 'operator_user', maxKeys: 10, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' },
  ];

  let userCredentials: (string | { serverId: string })[] = [];

  let testTools = [
    {
      name: 'stdio_env_mock__echo',
      description: 'Echo back input message from STDIO server',
      inputSchema: {
        type: 'object',
        properties: { message: { type: 'string', description: 'Message to echo' } },
        required: ['message'],
      },
    },
    {
      name: 'http_direct_mock__health',
      description: 'Check HTTP direct mock health',
      inputSchema: {
        type: 'object',
        properties: {},
      },
    },
    {
      name: 'sse_vault_mock__health',
      description: 'Check SSE vault mock health',
      inputSchema: {
        type: 'object',
        properties: {},
      },
    },
    {
      name: 'mock-docker__docker_ps',
      description: 'List running docker containers',
      inputSchema: {
        type: 'object',
        properties: { format: { type: 'string', description: 'Output format' } },
      },
    },
  ];

  let embeddingSettings = {
    dashboardTitle: 'Model Context Gateway',
    dashboardIcon: 'fa-solid fa-network-wired',
    embeddingProvider: 'local',
    embeddingModelDir: 'data/models',
    embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
    embeddingApiModel: 'all-MiniLM-L6-v2',
    embeddingApiKey: '',
    allowOpenClientRegistration: true,
    userMaxKeys: 5,
    globalMaxKeys: 100,
    masterKeySource: 'Configured',
  };

  let authProviders = options.providers ?? [
    {
      providerName: 'ActiveDirectory',
      displayName: 'Active Directory LDAP',
      isEnabled: true,
      configJson: JSON.stringify({
        server: 'ad.company.local',
        port: 636,
        useSsl: true,
        domain: 'company.local',
        baseDn: 'dc=company,dc=local',
        bindDn: 'cn=admin,dc=company,dc=local',
        bindPassword: 'adminpassword',
      }),
    },
    {
      providerName: 'Oidc',
      displayName: 'OIDC / Reverse Proxy Headers',
      isEnabled: true,
      userHeader: 'Remote-User',
      groupsHeader: 'Remote-Groups',
    },
  ];

  let secretProviders = options.secretProviders ?? [
    {
      providerName: 'Vault',
      displayName: 'HashiCorp Vault (KV v2)',
      isEnabled: true,
      configJson: JSON.stringify({
        address: 'https://vault.company.local:8200',
        mountPath: 'secret/data/',
      }),
    },
  ];

  await page.route(url => url.pathname.startsWith('/api/') || url.pathname === '/health', async (route: Route) => {
    const req = route.request();
    const urlObj = new URL(req.url());
    const path = urlObj.pathname;
    const method = req.method();

    if (path === '/health') {
      return route.fulfill({ json: { status: 'healthy', service: 'ModelContextGateway', version: '5.17.4' } });
    }

    if (path === '/api/config/branding') {
      return route.fulfill({ json: { title: embeddingSettings.dashboardTitle, logoUrl: embeddingSettings.dashboardIcon } });
    }

    if (path === '/api/user/me' || path === '/api/me') {
      if (isAdmin) {
        return route.fulfill({
          json: {
            username: 'admin',
            displayName: 'Administrator',
            groups: ['full_admin', 'house_member'],
            isAdmin: true,
          },
        });
      } else if (isOperator) {
        return route.fulfill({
          json: {
            username: 'operator_user',
            displayName: 'SmartHome Operator',
            groups: ['SmartHomeOperators'],
            isAdmin: false,
          },
        });
      } else {
        return route.fulfill({
          json: {
            username: 'guest_user',
            displayName: 'Guest User',
            groups: ['Guests'],
            isAdmin: false,
          },
        });
      }
    }

    // Servers
    if (path === '/api/servers') {
      if (method === 'GET') {
        return route.fulfill({ json: servers });
      }
      if (method === 'POST') {
        const postData = req.postDataJSON() || {};
        const newServer = {
          id: postData.id || postData.alias || `server_${Date.now()}`,
          displayName: postData.displayName || postData.name || 'New Server',
          type: postData.type || 'sse',
          url: postData.url || '',
          enabled: true,
          connectionStatus: 'Connected',
          categories: postData.categories || [],
          secretProvider: postData.secretProvider || 'None',
          toolsCount: 2,
          promptsCount: 1,
          resourcesCount: 0,
        };
        servers.push(newServer);
        testTools.push({
          name: `${newServer.id}__health`,
          description: `Health check for ${newServer.displayName}`,
          inputSchema: { type: 'object', properties: {} },
        });
        testTools.push({
          name: `${newServer.id}__echo`,
          description: `Echo tool for ${newServer.displayName}`,
          inputSchema: {
            type: 'object',
            properties: { message: { type: 'string', description: 'Message to echo' } },
            required: ['message'],
          },
        });
        return route.fulfill({ json: newServer });
      }
    }

    if (path.startsWith('/api/servers/') && path.endsWith('/inspect')) {
      const serverId = path.split('/')[3];
      return route.fulfill({
        json: {
          tools: [
            { name: `${serverId}__echo`, description: 'Echo back input message', inputSchema: { type: 'object', properties: { message: { type: 'string' } } } },
            { name: `${serverId}__status`, description: 'Get server status' },
          ],
          resources: [
            { name: `${serverId}__status_res`, uri: `${serverId}://status`, description: 'Server realtime status' },
          ],
          prompts: [
            { name: `${serverId}__diagnose`, description: 'Diagnose server status', arguments: [{ name: 'detail', required: true }] },
          ],
        },
      });
    }

    if (path.startsWith('/api/servers/') && method === 'DELETE') {
      const serverId = path.split('/')[3];
      servers = servers.filter(s => s.id !== serverId);
      return route.fulfill({ json: { success: true } });
    }

    // Tools, Prompts, Resources for TestBench
    if (path === '/api/test/tools') {
      return route.fulfill({ json: testTools });
    }

    if (path === '/api/test/prompts') {
      return route.fulfill({
        json: [
          {
            name: 'router__diagnose_failure',
            description: 'Diagnose router errors',
            arguments: [{ name: 'error_log', required: true, description: 'Log error stack' }],
          },
          {
            name: 'mock-docker__diagnose_container',
            description: 'Diagnose unhealthy container issues',
            arguments: [{ name: 'container_id', required: true, description: 'Target container ID' }],
          },
        ],
      });
    }

    if (path === '/api/test/resources') {
      return route.fulfill({
        json: {
          resources: [
            { name: 'Docker Status', uri: 'mcp://mock-docker/status', description: 'Docker engine status' },
            { name: 'Router System Status', uri: 'router://system_status.md', description: 'System status guide' },
          ],
          templates: [
            { name: 'Container Logs', uriTemplate: 'mcp://mock-docker/logs/{id}', description: 'Container log stream' },
          ],
        },
      });
    }

    if (path === '/api/test/call' || path === '/api/test/execute-tool') {
      const data = req.postDataJSON() || {};
      return route.fulfill({
        json: {
          content: [{ type: 'text', text: `Tool executed successfully with input: ${JSON.stringify(data.arguments || {})}` }],
          isError: false,
        },
      });
    }

    if (path === '/api/test/prompts/get' || path === '/api/test/get-prompt') {
      const data = req.postDataJSON() || {};
      return route.fulfill({
        json: {
          description: 'Rendered prompt successfully',
          messages: [{ role: 'user', content: { type: 'text', text: `Diagnose error log: ${data.arguments?.error_log || 'none'}` } }],
        },
      });
    }

    if (path === '/api/test/resources/read' || path === '/api/test/read-resource') {
      const data = req.postDataJSON() || {};
      return route.fulfill({
        json: {
          contents: [{ uri: data.uri || 'router://system_status.md', mimeType: 'text/plain', text: '# System Status\n\nAll systems nominal.' }],
        },
      });
    }

    if (path === '/api/test/semantic-search') {
      return route.fulfill({
        json: [
          {
            name: 'sse_vault_mock__health',
            description: 'Health status check',
            score: 0.96,
          },
          {
            name: 'mock-docker__docker_ps',
            description: 'List running docker containers',
            score: 0.85,
          },
        ],
      });
    }

    // User credentials
    if (path === '/api/user/credentials') {
      return route.fulfill({ json: userCredentials });
    }

    if (path.startsWith('/api/user/credentials/')) {
      const serverId = path.split('/')[4];
      if (method === 'POST') {
        if (!userCredentials.some(c => (typeof c === 'string' ? c === serverId : c.serverId === serverId))) {
          userCredentials.push({ serverId });
        }
        return route.fulfill({ json: { success: true } });
      }
      if (method === 'DELETE') {
        userCredentials = userCredentials.filter(c => (typeof c === 'string' ? c !== serverId : c.serverId !== serverId));
        return route.fulfill({ json: { success: true } });
      }
    }

    if (path === '/api/oauth/egress/servers') {
      return route.fulfill({ json: [] });
    }

    // AppKeys
    if (path === '/api/appkeys/limits') {
      return route.fulfill({
        json: {
          globalMax: 100,
          userMax: 10,
          totalActiveKeys: appKeys.length,
          userActiveKeys: appKeys.length,
          isLimitReached: false,
        },
      });
    }

    if (path === '/api/appkeys/quotas') {
      if (method === 'GET') {
        return route.fulfill({ json: quotas });
      }
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const existing = quotas.find(q => q.username === body.username);
        if (existing) {
          existing.maxKeys = body.maxKeys;
        } else {
          quotas.push({ username: body.username, maxKeys: body.maxKeys, createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() });
        }
        return route.fulfill({ json: { success: true } });
      }
    }

    if (path.startsWith('/api/appkeys/quotas/') && method === 'DELETE') {
      const username = path.split('/')[4];
      quotas = quotas.filter(q => q.username !== username);
      return route.fulfill({ json: { success: true } });
    }

    if (path === '/api/appkeys') {
      if (method === 'GET') {
        return route.fulfill({ json: appKeys });
      }
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const newKey = {
          id: `key-${Date.now()}`,
          name: body.name || 'Personal Key',
          username: isAdmin ? 'admin' : 'operator_user',
          keyType: body.keyType || 'personal',
          keyPrefix: 'mcg_live_demo99',
          rawKey: 'mcg_live_demo99_secret_token_1234567890abcdef',
          scopes: body.scopes || [],
          expiresAt: body.expiresAt || null,
          createdAt: new Date().toISOString(),
        };
        appKeys.push(newKey);
        return route.fulfill({ json: newKey });
      }
    }

    if (path.startsWith('/api/appkeys/') && method === 'DELETE') {
      const id = path.split('/')[3];
      appKeys = appKeys.filter(k => k.id !== id);
      return route.fulfill({ json: { success: true } });
    }

    // Clients
    if (path === '/api/clients') {
      if (method === 'GET') {
        return route.fulfill({ json: clients });
      }
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const newClient = {
          id: `client-${Date.now()}`,
          displayName: body.clientName || body.displayName || 'New Client',
          clientId: `client-${Date.now()}`,
          clientType: 'confidential',
          isDynamic: true,
          grantTypes: ['client_credentials'],
          redirectUris: body.redirectUris || [],
          scopes: body.scopes || ['mcp_client'],
          createdAt: new Date().toISOString(),
        };
        clients.push(newClient);
        return route.fulfill({ json: newClient });
      }
    }

    if (path.startsWith('/api/clients/') && method === 'DELETE') {
      const id = path.split('/')[3];
      clients = clients.filter(c => c.id !== id);
      return route.fulfill({ json: { success: true } });
    }

    // Settings
    if (path === '/api/settings/embedding') {
      if (method === 'GET') {
        return route.fulfill({ json: embeddingSettings });
      }
      if (method === 'PUT' || method === 'POST') {
        const body = req.postDataJSON() || {};
        embeddingSettings = { ...embeddingSettings, ...body };
        return route.fulfill({ json: { success: true, settings: embeddingSettings } });
      }
    }

    // Providers
    if (path === '/api/providers/auth') {
      if (method === 'GET') return route.fulfill({ json: authProviders });
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const idx = authProviders.findIndex(p => p.providerName === body.providerName);
        if (idx >= 0) authProviders[idx] = { ...authProviders[idx], ...body };
        else authProviders.push(body);
        return route.fulfill({ json: { success: true } });
      }
    }

    if (path === '/api/providers/secrets' || path === '/api/providers/secret') {
      if (method === 'GET') return route.fulfill({ json: secretProviders });
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const idx = secretProviders.findIndex(p => p.providerName === body.providerName);
        if (idx >= 0) secretProviders[idx] = { ...secretProviders[idx], ...body };
        else secretProviders.push(body);
        return route.fulfill({ json: { success: true } });
      }
    }

    // Test connection APIs
    if (path === '/api/settings/test-ldap') {
      return route.fulfill({ json: { success: true, message: 'LDAP connection successful!' } });
    }

    if (path === '/api/settings/test-vault' || path === '/api/providers/secrets/test-vault') {
      return route.fulfill({ json: { success: true, message: 'Vault connection successful!' } });
    }

    // Policies
    if (path === '/api/settings/policies' || path === '/api/permissions/policies') {
      if (method === 'GET') return route.fulfill({ json: policies });
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const newPolicy = {
          id: body.id || `pol-${Date.now()}`,
          targetId: body.targetId,
          requiredGroup: body.requiredGroup,
          isAllowed: body.isAllowed ?? true,
        };
        policies.push(newPolicy);
        return route.fulfill({ json: newPolicy });
      }
    }

    if ((path.startsWith('/api/settings/policies/') || path.startsWith('/api/permissions/policies/')) && method === 'DELETE') {
      const id = path.split('/')[4];
      policies = policies.filter(p => p.id !== id);
      return route.fulfill({ json: { success: true } });
    }

    // Mappings
    if (path === '/api/settings/mappings' || path === '/api/permissions/mappings') {
      if (method === 'GET') return route.fulfill({ json: mappings });
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const newMapping = {
          id: body.id || `map-${Date.now()}`,
          externalId: body.externalId,
          internalGroup: body.internalGroup,
        };
        mappings.push(newMapping);
        return route.fulfill({ json: newMapping });
      }
    }

    if ((path.startsWith('/api/settings/mappings/') || path.startsWith('/api/permissions/mappings/')) && method === 'DELETE') {
      const id = path.split('/')[4];
      mappings = mappings.filter(m => m.id !== id);
      return route.fulfill({ json: { success: true } });
    }

    // Custom files
    if (path === '/api/settings/customfiles') {
      if (method === 'GET') return route.fulfill({ json: customFiles });
      if (method === 'POST') {
        const body = req.postDataJSON() || {};
        const existing = customFiles.find(f => f.type === body.type && f.name === body.name);
        if (existing) {
          existing.content = body.content;
          existing.sizeBytes = (body.content || '').length;
        } else {
          customFiles.push({
            type: body.type,
            name: body.name,
            content: body.content,
            sizeBytes: (body.content || '').length,
            lastModified: new Date().toISOString(),
          });
        }
        return route.fulfill({ json: { success: true } });
      }
    }

    if (path.startsWith('/api/settings/customfiles/') && method === 'DELETE') {
      const parts = path.split('/');
      const type = parts[4];
      const name = parts[5];
      customFiles = customFiles.filter(f => !(f.type === type && f.name === name));
      return route.fulfill({ json: { success: true } });
    }

    if (path === '/api/logs' || path.startsWith('/api/my-mcp')) {
      return route.fulfill({ json: [] });
    }

    // Default fallback for any unhandled /api call
    return route.fulfill({ json: {} });
  });
}
