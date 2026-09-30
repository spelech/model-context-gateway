import { create } from 'zustand';
import { showToast } from './useToastStore';
import { confirmAction } from './useConfirmStore';
import {
  AppKeyItem,
  AppKey,
  AppKeyLimits,
  NewAppKeyResult,
  CreateAppKeyPayload,
  CreateAppKeyRequest,
  UserQuota
} from '../shared/types';
import {
  fetchAppKeysApi,
  fetchAppKeyLimitsApi,
  createAppKeyApi,
  revokeAppKeyApi,
  rotateAppKeyApi
} from '../api/appKeyApi';
import {
  fetchUserQuotasApi,
  setUserQuotaApi,
  deleteUserQuotaApi
} from '../api/userQuotaApi';

export type {
  AppKeyItem,
  AppKey,
  AppKeyLimits,
  NewAppKeyResult,
  CreateAppKeyPayload,
  CreateAppKeyRequest,
  UserQuota
};

interface AppKeyStore {
  appKeys: AppKeyItem[];
  limits: AppKeyLimits | null;
  keyTypeTab: 'personal' | 'system';
  userQuotas: UserQuota[];
  isLoading: boolean;
  isLoadingQuotas: boolean;
  isRotating: boolean;
  isCreateModalOpen: boolean;
  createdResult: NewAppKeyResult | null;

  setKeyTypeTab: (tab: 'personal' | 'system') => void;
  fetchAppKeys: (keyType?: string, usernameFilter?: string) => Promise<void>;
  fetchLimits: () => Promise<void>;
  createAppKey: (payload: CreateAppKeyPayload) => Promise<void>;
  revokeAppKey: (id: string, name: string) => Promise<void>;
  rotateAppKey: (id: string) => Promise<{ id: string; name: string; keyPrefix: string; plaintextKey: string }>;
  fetchUserQuotas: () => Promise<void>;
  setUserQuota: (username: string, maxKeys: number) => Promise<void>;
  deleteUserQuota: (username: string) => Promise<void>;
  openModal: () => void;
  closeModal: () => void;
}

export const useAppKeyStore = create<AppKeyStore>((set, get) => ({
  appKeys: [],
  limits: null,
  keyTypeTab: 'personal',
  userQuotas: [],
  isLoading: false,
  isLoadingQuotas: false,
  isRotating: false,
  isCreateModalOpen: false,
  createdResult: null,

  setKeyTypeTab: (tab: 'personal' | 'system') => set({ keyTypeTab: tab }),

  fetchAppKeys: async (keyType?: string, usernameFilter?: string) => {
    set({ isLoading: true });
    try {
      const data = await fetchAppKeysApi(keyType, usernameFilter);
      set({ appKeys: data || [], isLoading: false });
    } catch (e: any) {
      console.error('Error fetching app keys:', e);
      set({ isLoading: false });
    }
  },

  fetchLimits: async () => {
    try {
      const data = await fetchAppKeyLimitsApi();
      set({ limits: data });
    } catch (e: any) {
      console.error('Error fetching app key limits:', e);
    }
  },

  createAppKey: async (payload) => {
    try {
      const result = await createAppKeyApi(payload);
      set({ createdResult: result });
      showToast('App Key created successfully', 'success');
      get().fetchAppKeys(payload.keyType || get().keyTypeTab);
      get().fetchLimits();
    } catch (e: any) {
      showToast(`Error creating App Key: ${e.message}`, 'error');
      throw e;
    }
  },

  revokeAppKey: async (id, name) => {
    const confirmed = await confirmAction({
      title: 'Revoke App Key',
      message: `Are you sure you want to revoke the App Key '${name}'? This cannot be undone.`,
      confirmText: 'Revoke Key',
      danger: true
    });
    if (!confirmed) return;
    try {
      await revokeAppKeyApi(id);
      showToast('App Key revoked successfully', 'success');
      get().fetchAppKeys(get().keyTypeTab);
      get().fetchLimits();
    } catch (e: any) {
      showToast(`Error revoking App Key: ${e.message}`, 'error');
    }
  },

  rotateAppKey: async (id: string) => {
    set({ isRotating: true });
    try {
      const result = await rotateAppKeyApi(id);
      set((state) => ({
        isRotating: false,
        appKeys: state.appKeys.map((k) =>
          k.id === id
            ? { ...k, keyPrefix: result.keyPrefix, lastUsedAt: null, LastUsedAt: null }
            : k
        )
      }));
      showToast('App Key rotated successfully', 'success');
      return {
        id: result.id,
        name: result.name,
        keyPrefix: result.keyPrefix,
        plaintextKey: result.plaintextKey
      };
    } catch (e: any) {
      set({ isRotating: false });
      showToast(`Error rotating App Key: ${e.message}`, 'error');
      throw e;
    }
  },

  fetchUserQuotas: async () => {
    set({ isLoadingQuotas: true });
    try {
      const data = await fetchUserQuotasApi();
      set({ userQuotas: data || [], isLoadingQuotas: false });
    } catch (e: any) {
      console.error('Error fetching user quotas:', e);
      set({ isLoadingQuotas: false });
    }
  },

  setUserQuota: async (username: string, maxKeys: number) => {
    try {
      await setUserQuotaApi(username, maxKeys);
      showToast(`Quota updated for ${username}`, 'success');
      get().fetchUserQuotas();
    } catch (e: any) {
      showToast(`Error updating quota: ${e.message}`, 'error');
      throw e;
    }
  },

  deleteUserQuota: async (username: string) => {
    const confirmed = await confirmAction({
      title: 'Reset User Quota',
      message: `Are you sure you want to reset the quota for '${username}' back to the default?`,
      confirmText: 'Reset Quota',
      danger: true
    });
    if (!confirmed) return;
    try {
      await deleteUserQuotaApi(username);
      showToast(`Quota reset for ${username}`, 'success');
      get().fetchUserQuotas();
    } catch (e: any) {
      showToast(`Error deleting quota: ${e.message}`, 'error');
    }
  },

  openModal: () => set({ isCreateModalOpen: true, createdResult: null }),
  closeModal: () => set({ isCreateModalOpen: false, createdResult: null })
}));
