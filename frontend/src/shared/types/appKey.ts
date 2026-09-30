export interface AppKeyItem {
  id: string;
  name: string;
  username: string;
  keyType?: 'personal' | 'system';
  keyPrefix: string;
  scopes: string[];
  expiresAt?: string;
  createdAt: string;
  lastUsedAt?: string | null;
  LastUsedAt?: string | null;
}

export interface AppKeyLimits {
  globalMax: number;
  userMax: number;
  totalActiveKeys: number;
  userActiveKeys: number;
  isLimitReached: boolean;
}

export interface NewAppKeyResult {
  id: string;
  name: string;
  username: string;
  keyType?: 'personal' | 'system';
  keyPrefix: string;
  plaintextKey: string;
  scopes: string[];
  expiresAt?: string;
  createdAt: string;
  lastUsedAt?: string | null;
  LastUsedAt?: string | null;
}

export interface CreateAppKeyPayload {
  name: string;
  username?: string;
  keyType?: 'personal' | 'system';
  scopes: string[];
  expiresInDays?: number;
}

export interface UserQuota {
  username: string;
  maxKeys: number;
  createdAt: string;
  updatedAt: string;
}

export interface SetUserQuotaPayload {
  username: string;
  maxKeys: number;
}

export type AppKey = AppKeyItem;
export type CreateAppKeyRequest = CreateAppKeyPayload;
export type SetUserQuotaRequest = SetUserQuotaPayload;
