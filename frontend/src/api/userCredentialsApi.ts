import { apiRequest } from '../shared/api/api';

export type UserCredential = string | { serverId: string };

export async function fetchUserCredentialsApi(): Promise<UserCredential[]> {
  const data = await apiRequest<any>('/api/user/credentials');
  return data || [];
}

export async function saveUserCredentialApi(serverId: string, secretJson: string): Promise<void> {
  let parsed: any = secretJson;
  try { parsed = JSON.parse(secretJson); } catch { /* keep as string */ }
  await apiRequest(`/api/user/credentials/${serverId}`, {
    method: 'POST',
    body: JSON.stringify({ secretJson: parsed }),
    headers: {
      'Content-Type': 'application/json'
    }
  });
}

export async function deleteUserCredentialApi(serverId: string): Promise<void> {
  await apiRequest(`/api/user/credentials/${serverId}`, {
    method: 'DELETE'
  });
}

export interface OAuthServerInfo {
  id: string;
  alias?: string;
  displayName: string;
  enableOAuth3Lo: boolean;
  isConnected: boolean;
}

export async function fetchOAuthServersApi(): Promise<OAuthServerInfo[]> {
  const data = await apiRequest<OAuthServerInfo[]>('/api/oauth/egress/servers');
  return data || [];
}

export async function disconnectOAuthAccountApi(serverId: string): Promise<void> {
  await apiRequest(`/api/oauth/egress/disconnect/${serverId}`, {
    method: 'POST'
  });
}
