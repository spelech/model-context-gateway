export interface RegisteredClient {
  id: string;
  clientId: string;
  displayName: string;
  clientType?: 'confidential' | 'public';
  redirectUris?: string[];
  grantTypes?: string[];
  scopes: string[];
  expiresAt: string | null;
  createdAt: string;
  isDynamic?: boolean;
  lastUsedAt?: string | null;
  LastUsedAt?: string | null;
}

export interface NewClientResult {
  id?: string;
  clientId: string;
  clientSecret: string;
  displayName?: string;
  scopes?: string[];
  redirectUris?: string[];
  grantTypes?: string[];
  expiresAt?: string | null;
}
