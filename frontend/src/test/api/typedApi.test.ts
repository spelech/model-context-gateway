/** @requirement UI-119 */

import { describe, it, expect } from 'vitest';
import { mockApiResponse } from '../setup';
import {
  fetchServersApi,
  reconnectAllServersApi,
  toggleServerEnabledApi,
  reconnectServerApi,
  createServerApi,
  updateServerApi,
  deleteServerApi,
  inspectServerApi,
} from '../../api/serverApi';
import { fetchClientsApi, registerClientApi, deleteClientApi, cleanupClientsApi } from '../../api/clientApi';
import { fetchAppKeysApi, fetchAppKeyLimitsApi, createAppKeyApi, revokeAppKeyApi } from '../../api/appKeyApi';
import { fetchUserQuotasApi, setUserQuotaApi, deleteUserQuotaApi } from '../../api/userQuotaApi';
import { fetchPoliciesApi, savePolicyApi, deletePolicyApi, fetchMappingsApi, saveMappingApi, deleteMappingApi } from '../../api/securityApi';
import {
  fetchEmbeddingSettingsApi,
  saveEmbeddingSettingsApi,
  fetchAuthProvidersApi,
  saveAuthProviderApi,
  fetchSecretProvidersApi,
  saveSecretProviderApi,
  fetchCustomFilesApi,
  fetchCustomFileContentApi,
  saveCustomFileApi,
  deleteCustomFileApi,
  uploadBrandingLogo,
} from '../../api/settingsApi';
import {
  fetchTestToolsApi,
  fetchTestPromptsApi,
  fetchTestResourcesApi,
  fetchLogsApi,
  clearLogsApi,
  executeToolApi,
  getPromptApi,
  readResourceApi,
  semanticSearchApi
} from '../../api/testbenchApi';

describe('Typed API Client Layer', () => {
  describe('serverApi', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description calls server endpoints correctly
     */
    it('calls server endpoints correctly', async () => {
      mockApiResponse('/api/servers', [{ id: 's1', displayName: 'Server 1' }]);
      mockApiResponse('/api/servers/reconnect-all', { success: true });
      mockApiResponse(/\/api\/servers\/s1/, { success: true });
      mockApiResponse('/api/servers/s1/reconnect', { success: true });
      mockApiResponse('/api/servers/s1/inspect', { tools: [], resources: [], prompts: [] });

      const servers = await fetchServersApi();
      expect(servers).toHaveLength(1);

      await reconnectAllServersApi();
      await toggleServerEnabledApi('s1', true);
      await reconnectServerApi('s1');
      await createServerApi({ displayName: 'New Server', type: 'sse', categories: [], url: 'http://test', secretProvider: 'None', secretItemKey: '', authShape: 'bearer', customHeaderName: '', enabled: true, hidden: false });
      await updateServerApi('s1', { displayName: 'Updated' });
      await deleteServerApi('s1');
      const inspect = await inspectServerApi('s1');
      expect(inspect).toBeDefined();
    });
  });

  describe('clientApi and appKeyApi', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description calls client and appkey endpoints correctly
     */
    it('calls client and appkey endpoints correctly', async () => {
      mockApiResponse('/api/clients', [{ id: 'c1', clientId: 'client-1', displayName: 'Client 1', isDynamic: false }]);
      mockApiResponse('/api/clients/c1', { success: true });
      mockApiResponse('/api/appkeys', (url) => {
        if (url.includes('keyType=system')) {
          return [{ id: 'k2', name: 'Key 2', username: 'admin', keyType: 'system', keyPrefix: 'mcp_live_456', scopes: ['all'], createdAt: '' }];
        }
        return [{ id: 'k1', name: 'Key 1', username: 'admin', keyType: 'personal', keyPrefix: 'mcp_live_123', scopes: ['all'], createdAt: '' }];
      });
      mockApiResponse('/api/appkeys/limits', { globalMax: 50, userMax: 10, totalActiveKeys: 1, userActiveKeys: 1, isLimitReached: false });
      mockApiResponse(/\/api\/appkeys\/k1/, { success: true });

      const clients = await fetchClientsApi();
      expect(clients).toHaveLength(1);

      await registerClientApi(
        'Test Client',
        ['all'],
        ['https://oauth.pstmn.io/v1/callback'],
        ['authorization_code', 'client_credentials'],
        'confidential',
        30
      );
      await deleteClientApi('c1');

      mockApiResponse(/\/api\/clients\/cleanup/, { cleanedCount: 2, message: 'Cleaned up 2' });
      const cleanupRes = await cleanupClientsApi(30);
      expect(cleanupRes.cleanedCount).toBe(2);

      const appKeys = await fetchAppKeysApi();
      expect(appKeys).toHaveLength(1);

      const filteredKeys = await fetchAppKeysApi('system', 'admin');
      expect(filteredKeys).toHaveLength(1);
      expect(filteredKeys[0].keyType).toBe('system');

      const limits = await fetchAppKeyLimitsApi();
      expect(limits.globalMax).toBe(50);

      await createAppKeyApi({ name: 'New Key', keyType: 'personal', scopes: ['all'] });
      await revokeAppKeyApi('k1');
    });
  });

  describe('userQuotaApi', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description calls user quota endpoints correctly
     */
    it('calls user quota endpoints correctly', async () => {
      mockApiResponse('/api/appkeys/quotas', (_url, options) => {
        if (options?.method === 'POST') {
          return { success: true, username: 'bob', maxKeys: 8 };
        }
        return [
          { username: 'alice', maxKeys: 10, createdAt: '2026-08-22T00:00:00Z', updatedAt: '2026-08-22T00:00:00Z' }
        ];
      });
      mockApiResponse(/\/api\/appkeys\/quotas\/alice/, { success: true, username: 'alice' });

      const quotas = await fetchUserQuotasApi();
      expect(quotas).toHaveLength(1);
      expect(quotas[0].username).toBe('alice');

      const setResult = await setUserQuotaApi('bob', 8);
      expect(setResult.success).toBe(true);

      await deleteUserQuotaApi('alice');
    });
  });

  describe('securityApi', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description calls policies and mappings endpoints correctly
     */
    it('calls policies and mappings endpoints correctly', async () => {
      mockApiResponse('/api/permissions/policies', [{ id: 'p1', targetId: 'server:ha', requiredGroup: 'admins', isAllowed: true }]);
      mockApiResponse(/\/api\/permissions\/policies\/p1/, { success: true });
      mockApiResponse('/api/permissions/mappings', [{ id: 'm1', externalId: 'ext_admin', internalGroup: 'Administrators' }]);
      mockApiResponse(/\/api\/permissions\/mappings\/m1/, { success: true });

      const policies = await fetchPoliciesApi();
      expect(policies).toHaveLength(1);

      await savePolicyApi({ targetId: 'server:ha', requiredGroup: 'admins', isAllowed: true });
      await deletePolicyApi('p1');

      const mappings = await fetchMappingsApi();
      expect(mappings).toHaveLength(1);

      await saveMappingApi({ externalId: 'ext', internalGroup: 'grp' });
      await deleteMappingApi('m1');
    });
  });

  describe('settingsApi', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description calls settings, providers, custom files, approvals endpoints correctly
     */
    it('calls settings, providers, custom files, approvals endpoints correctly', async () => {
      mockApiResponse('/api/settings', (_url, options) => {
        if (options?.method === 'POST') {
          return { success: true };
        }
        return { embeddingProvider: 'local', embeddingModelDir: 'data/models' };
      });
      mockApiResponse('/api/providers/auth', []);
      mockApiResponse('/api/providers/secrets', []);
      mockApiResponse('/api/custom-files', []);
      mockApiResponse('/api/custom-files/prompts/test.json', { content: '{}' });
      mockApiResponse('/api/approvals', []);
      mockApiResponse('/api/approvals/app-1/action', { success: true });

      const settings = await fetchEmbeddingSettingsApi();
      expect(settings?.embeddingProvider).toBe('local');

      const saveSettingsRes = await saveEmbeddingSettingsApi({
        embeddingProvider: 'local',
        embeddingModelDir: 'data/models',
        embeddingApiUrl: '',
        embeddingApiModel: 'all-MiniLM-L6-v2',
        embeddingApiKey: '',
      });
      expect(saveSettingsRes).toBe(true);

      const auth = await fetchAuthProvidersApi();
      expect(auth).toEqual([]);
      await saveAuthProviderApi({ providerName: 'AD', displayName: 'Active Directory', isEnabled: true });

      const secrets = await fetchSecretProvidersApi();
      expect(secrets).toEqual([]);
      await saveSecretProviderApi({ providerName: 'Vault', displayName: 'Vault', isEnabled: true, configJson: '{}' });

      const files = await fetchCustomFilesApi();
      expect(files).toEqual([]);
      const fileContent = await fetchCustomFileContentApi('prompts', 'test.json');
      expect(fileContent).toBe('{}');

      mockApiResponse('/api/config/branding/logo', { url: '/api/config/branding/logo', success: true });
      const logoUpload = await uploadBrandingLogo(new File(['test'], 'logo.png', { type: 'image/png' }));
      expect(logoUpload.success).toBe(true);
      expect(logoUpload.url).toBe('/api/config/branding/logo');

      await saveCustomFileApi('prompts', 'test.json', '{}');
      await deleteCustomFileApi('prompts', 'test.json');
    });
  });

  describe('testbenchApi', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description calls testbench tool, prompt, resource, log endpoints correctly
     */
    it('calls testbench tool, prompt, resource, log endpoints correctly', async () => {
      mockApiResponse('/api/test/tools', [{ name: 'tool1', description: 'desc' }]);
      mockApiResponse('/api/test/prompts', [{ name: 'prompt1', description: 'desc' }]);
      mockApiResponse('/api/test/resources', { resources: [], templates: [] });
      mockApiResponse('/api/logs', (_url, options) => {
        if (options?.method === 'DELETE') {
          return { success: true };
        }
        return [];
      });

      const tools = await fetchTestToolsApi();
      expect(tools).toHaveLength(1);

      const prompts = await fetchTestPromptsApi();
      expect(prompts).toHaveLength(1);

      const resources = await fetchTestResourcesApi();
      expect(resources.resources).toEqual([]);

      const logs = await fetchLogsApi();
      expect(logs).toEqual([]);

      await clearLogsApi();

      mockApiResponse('/api/test/call', { result: 'tool_output' });
      const toolRes = await executeToolApi('srv1', 'tool1', { arg: 'val' });
      expect(toolRes.result).toBe('tool_output');

      mockApiResponse('/api/test/prompts/get', { prompt: 'prompt_output' });
      const promptRes = await getPromptApi('srv1', 'prompt1', { p: 1 });
      expect(promptRes.prompt).toBe('prompt_output');

      mockApiResponse('/api/test/resources/read', { content: 'res_output' });
      const resResult = await readResourceApi('srv1', 'mcp://srv1/res');
      expect(resResult.content).toBe('res_output');

      mockApiResponse('/api/test/semantic-search', {
        query: 'query',
        mode: 'hybrid',
        denseWeight: 0.5,
        results: [{ toolName: 'tool1', score: 0.9 }],
      });
      const searchRes = await semanticSearchApi('query');
      expect(searchRes.results).toHaveLength(1);
    });
  });
});
