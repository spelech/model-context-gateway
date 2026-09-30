import { apiRequest } from '../shared/api/api';
import { AppKeyItem, AppKeyLimits, NewAppKeyResult, CreateAppKeyPayload } from '../shared/types';

export async function fetchAppKeysApi(keyType?: string, usernameFilter?: string): Promise<AppKeyItem[]> {
  const params = new URLSearchParams();
  if (keyType) {
    params.append('keyType', keyType);
  }
  if (usernameFilter) {
    params.append('usernameFilter', usernameFilter);
  }
  const queryString = params.toString();
  const endpoint = queryString ? `/api/appkeys?${queryString}` : '/api/appkeys';
  const data = await apiRequest<AppKeyItem[]>(endpoint);
  return data || [];
}

export async function fetchAppKeyLimitsApi(): Promise<AppKeyLimits> {
  return apiRequest<AppKeyLimits>('/api/appkeys/limits');
}

export async function createAppKeyApi(payload: CreateAppKeyPayload): Promise<NewAppKeyResult> {
  return apiRequest<NewAppKeyResult>('/api/appkeys', {
    method: 'POST',
    body: payload
  });
}

export async function revokeAppKeyApi(id: string): Promise<void> {
  await apiRequest(`/api/appkeys/${id}`, { method: 'DELETE' });
}

export async function rotateAppKeyApi(id: string): Promise<NewAppKeyResult> {
  return apiRequest<NewAppKeyResult>(`/api/appkeys/${id}/rotate`, {
    method: 'POST'
  });
}

