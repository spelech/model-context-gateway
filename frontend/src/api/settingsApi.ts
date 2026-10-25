import { apiRequest } from '../shared/api/api';
import {
  EmbeddingSettings,
  AuthProviderConfig,
  SecretProviderConfig,
  CustomFileMeta
} from '../shared/types';

export async function fetchEmbeddingSettingsApi(): Promise<EmbeddingSettings | null> {
  const settings = await apiRequest<any>('/api/settings');
  if (!settings) return null;
  return {
    dashboardTitle: settings.dashboardTitle || 'Model Context Gateway',
    dashboardIcon: settings.dashboardIcon || 'fa-solid fa-network-wired',
    embeddingProvider: settings.embeddingProvider || 'local',
    embeddingModelDir: settings.embeddingModelDir || 'data/models',
    embeddingApiUrl: settings.embeddingApiUrl || '',
    embeddingApiModel: settings.embeddingApiModel || 'all-MiniLM-L6-v2',
    embeddingApiKey: settings.embeddingApiKey || '',
    allowOpenClientRegistration: settings.allowOpenClientRegistration ?? true,
    globalMaxKeys: settings.globalMaxKeys ?? 0,
    userMaxKeys: settings.userMaxKeys ?? 0,
    masterKeySource: settings.masterKeySource,
    toolSearchDefaultLimit: settings.toolSearchDefaultLimit ?? 5,
    toolSearchMinScore: settings.toolSearchMinScore ?? 0.35,
    toolSearchScoreDropoffRatio: settings.toolSearchScoreDropoffRatio ?? 0.60,
    enableCodeMode: settings.enableCodeMode ?? true,
    codeModeExecutionTimeoutSeconds: settings.codeModeExecutionTimeoutSeconds ?? 30,
    codeModeMaxToolCalls: settings.codeModeMaxToolCalls ?? 50,
    codeModeMaxMemoryMb: settings.codeModeMaxMemoryMb ?? 64,
    codeModeMaxStatements: settings.codeModeMaxStatements ?? 100000,
    codeModeAllowedGroups: settings.codeModeAllowedGroups ?? '',
  };
}

export async function saveEmbeddingSettingsApi(settings: EmbeddingSettings): Promise<boolean> {
  const payload = {
    dashboardTitle: settings.dashboardTitle,
    dashboardIcon: settings.dashboardIcon,
    embeddingProvider: settings.embeddingProvider,
    embeddingModelDir: settings.embeddingModelDir,
    embeddingApiUrl: settings.embeddingApiUrl,
    embeddingApiModel: settings.embeddingApiModel,
    embeddingApiKey: settings.embeddingApiKey,
    allowOpenClientRegistration: settings.allowOpenClientRegistration,
    globalMaxKeys: settings.globalMaxKeys,
    userMaxKeys: settings.userMaxKeys,
    toolSearchDefaultLimit: settings.toolSearchDefaultLimit,
    toolSearchMinScore: settings.toolSearchMinScore,
    toolSearchScoreDropoffRatio: settings.toolSearchScoreDropoffRatio,
    enableCodeMode: settings.enableCodeMode,
    codeModeExecutionTimeoutSeconds: settings.codeModeExecutionTimeoutSeconds,
    codeModeMaxToolCalls: settings.codeModeMaxToolCalls,
    codeModeMaxMemoryMb: settings.codeModeMaxMemoryMb,
    codeModeMaxStatements: settings.codeModeMaxStatements,
    codeModeAllowedGroups: settings.codeModeAllowedGroups,
  };
  const result = await apiRequest<{ success: boolean }>('/api/settings', {
    method: 'POST',
    body: payload
  });
  return !!(result && result.success);
}

export async function fetchAuthProvidersApi(): Promise<AuthProviderConfig[]> {
  const data = await apiRequest<AuthProviderConfig[]>('/api/providers/auth');
  return data || [];
}

export async function saveAuthProviderApi(provider: AuthProviderConfig): Promise<void> {
  await apiRequest('/api/providers/auth', {
    method: 'POST',
    body: provider
  });
}

export async function testLdapConnectionApi(config: {
  server: string;
  port?: number;
  useSsl?: boolean;
  domain?: string;
  baseDn?: string;
  bindDn?: string;
  bindPassword?: string;
}): Promise<{ success: boolean; message?: string; error?: string }> {
  const res = await apiRequest<{ success: boolean; message?: string; error?: string }>('/api/providers/auth/test-ad', {
    method: 'POST',
    body: config
  });
  return res || { success: false, error: 'Network error communicating with server.' };
}

export async function fetchSecretProvidersApi(): Promise<SecretProviderConfig[]> {
  const data = await apiRequest<SecretProviderConfig[]>('/api/providers/secrets');
  return data || [];
}

export async function saveSecretProviderApi(provider: SecretProviderConfig): Promise<void> {
  await apiRequest('/api/providers/secrets', {
    method: 'POST',
    body: provider
  });
}

export async function testVaultConnectionApi(config: {
  address: string;
  authMethod?: string;
  token?: string;
  roleId?: string;
  secretId?: string;
  mountPath?: string;
}): Promise<{ success: boolean; message?: string; error?: string }> {
  const res = await apiRequest<{ success: boolean; message?: string; error?: string }>('/api/providers/secrets/test-vault', {
    method: 'POST',
    body: config
  });
  return res || { success: false, error: 'Network error communicating with server.' };
}

export async function fetchCustomFilesApi(): Promise<CustomFileMeta[]> {
  const files = await apiRequest<CustomFileMeta[]>('/api/custom-files');
  return files || [];
}

export async function fetchCustomFileContentApi(type: 'prompts' | 'resources', name: string): Promise<string> {
  const res = await apiRequest<{ content: string }>(`/api/custom-files/${type}/${name}`);
  return res?.content || '';
}

export async function saveCustomFileApi(type: 'prompts' | 'resources', name: string, content: string): Promise<boolean> {
  const result = await apiRequest<{ success: boolean }>(`/api/custom-files/${type}/${name}`, {
    method: 'POST',
    body: { content }
  });
  return !!(result && result.success);
}

export async function deleteCustomFileApi(type: 'prompts' | 'resources', name: string): Promise<boolean> {
  const result = await apiRequest<{ success: boolean }>(`/api/custom-files/${type}/${name}`, {
    method: 'DELETE'
  });
  return !!(result && result.success);
}

export async function uploadBrandingLogo(file: File): Promise<{ url: string; success: boolean }> {
  const formData = new FormData();
  formData.append('file', file);
  const result = await apiRequest<{ url: string; success: boolean }>('/api/config/branding/logo', {
    method: 'POST',
    body: formData
  });
  return result || { url: '', success: false };
}

export async function setMasterKeyApi(newKey: string): Promise<{ success: boolean; message?: string; keySource?: string; error?: string }> {
  const result = await apiRequest<{ success: boolean; message?: string; keySource?: string; error?: string }>('/api/config/master-key', {
    method: 'POST',
    body: { newKey }
  });
  return result || { success: false, error: 'Failed to update master key.' };
}

