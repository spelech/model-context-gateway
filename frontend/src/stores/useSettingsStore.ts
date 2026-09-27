import { create } from 'zustand';
import { showToast } from './useToastStore';
import { confirmAction } from './useConfirmStore';
import {
  EmbeddingSettings,
  AuthProviderConfig,
  SecretProviderConfig,
  CustomFileMeta,
  AccessPolicy,
  GroupMapping,
} from '../shared/types';
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
  setMasterKeyApi,
} from '../api/settingsApi';
import {
  fetchPoliciesApi,
  savePolicyApi,
  deletePolicyApi,
  fetchMappingsApi,
  saveMappingApi,
  deleteMappingApi,
} from '../api/securityApi';

export type {
  EmbeddingSettings,
  AuthProviderConfig,
  SecretProviderConfig,
  CustomFileMeta,
  AccessPolicy,
  GroupMapping,
};

interface SettingsStore {
  embeddingSettings: EmbeddingSettings | null;
  authProviders: AuthProviderConfig[];
  secretProviders: SecretProviderConfig[];
  customFiles: CustomFileMeta[];
  policies: AccessPolicy[];
  mappings: GroupMapping[];

  isLoadingSettings: boolean;
  isSavingSettings: boolean;

  // Active custom file modal editor state
  isCustomFileOpen: boolean;
  editingFileMeta: CustomFileMeta | null; // null for Create
  editingFileContent: string;
  activeFileModalTab: 'editor' | 'builder';

  // Policy Modal
  isPolicyModalOpen: boolean;
  editingPolicy: AccessPolicy | null;

  // Mapping Modal
  isMappingModalOpen: boolean;
  editingMapping: GroupMapping | null;

  // Actions
  fetchEmbeddingSettings: () => Promise<void>;
  saveEmbeddingSettings: (settings: EmbeddingSettings) => Promise<boolean>;
  setMasterKey: (newKey: string) => Promise<{ success: boolean; message?: string; error?: string }>;

  fetchProviders: () => Promise<void>;
  saveAuthProvider: (provider: AuthProviderConfig) => Promise<void>;
  saveSecretProvider: (provider: SecretProviderConfig) => Promise<void>;

  fetchCustomFiles: () => Promise<void>;
  fetchCustomFileContent: (type: 'prompts' | 'resources', name: string) => Promise<string>;
  saveCustomFile: (type: 'prompts' | 'resources', name: string, content: string) => Promise<boolean>;
  deleteCustomFile: (type: 'prompts' | 'resources', name: string) => Promise<void>;

  fetchPolicies: () => Promise<void>;
  savePolicy: (policy: AccessPolicy) => Promise<void>;
  deletePolicy: (id: string) => Promise<void>;

  fetchMappings: () => Promise<void>;
  saveMapping: (mapping: GroupMapping) => Promise<void>;
  deleteMapping: (id: string) => Promise<void>;

  // UI state actions
  openCustomFileModal: (meta?: CustomFileMeta) => Promise<void>;
  closeCustomFileModal: () => void;
  setEditingFileContent: (content: string) => void;
  setActiveFileModalTab: (tab: 'editor' | 'builder') => void;

  openPolicyModal: (policy?: AccessPolicy) => void;
  closePolicyModal: () => void;

  openMappingModal: (mapping?: GroupMapping) => void;
  closeMappingModal: () => void;
}

export const useSettingsStore = create<SettingsStore>((set, get) => ({
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
  editingMapping: null,

  fetchEmbeddingSettings: async () => {
    set({ isLoadingSettings: true });
    try {
      const settings = await fetchEmbeddingSettingsApi();
      if (settings) {
        set({ embeddingSettings: settings });
      }
    } catch (e) {
      console.error('Failed to load settings:', e);
    } finally {
      set({ isLoadingSettings: false });
    }
  },

  saveEmbeddingSettings: async (settings) => {
    set({ isSavingSettings: true });
    try {
      const success = await saveEmbeddingSettingsApi(settings);
      if (success) {
        set({ embeddingSettings: settings });
        showToast('Settings saved successfully', 'success');
        return true;
      }
      return false;
    } catch (e: any) {
      console.error('Failed to save settings:', e);
      showToast(`Error saving settings: ${e.message}`, 'error');
      return false;
    } finally {
      set({ isSavingSettings: false });
    }
  },

  setMasterKey: async (newKey: string) => {
    try {
      const res = await setMasterKeyApi(newKey);
      if (res && res.success) {
        showToast(res.message || 'Master encryption key updated and database re-encrypted successfully.', 'success');
        await get().fetchEmbeddingSettings();
        return { success: true, message: res.message };
      } else {
        const errorMsg = res?.error || res?.message || 'Failed to update master key.';
        showToast(errorMsg, 'error');
        return { success: false, error: errorMsg };
      }
    } catch (err: any) {
      const errorMsg = err?.message || 'Failed to update master key.';
      showToast(errorMsg, 'error');
      return { success: false, error: errorMsg };
    }
  },

  fetchProviders: async () => {
    try {
      const auth = await fetchAuthProvidersApi();
      const secrets = await fetchSecretProvidersApi();
      set({
        authProviders: auth || [],
        secretProviders: secrets || []
      });
    } catch (e) {
      console.warn('Providers not yet initialized or endpoints unavailable:', e);
    }
  },

  saveAuthProvider: async (provider) => {
    try {
      await saveAuthProviderApi(provider);
      get().fetchProviders();
    } catch (e: any) {
      showToast(`Failed to save Auth Provider: ${e.message}`, 'error');
      throw e;
    }
  },

  saveSecretProvider: async (provider) => {
    try {
      await saveSecretProviderApi(provider);
      get().fetchProviders();
    } catch (e: any) {
      showToast(`Failed to save Secret Provider: ${e.message}`, 'error');
      throw e;
    }
  },

  fetchCustomFiles: async () => {
    try {
      const files = await fetchCustomFilesApi();
      set({ customFiles: files || [] });
    } catch (err) {
      console.error('Failed to fetch custom files:', err);
    }
  },

  fetchCustomFileContent: async (type, name) => {
    try {
      return await fetchCustomFileContentApi(type, name);
    } catch (err) {
      console.error('Failed to fetch custom file content:', err);
      return '';
    }
  },

  saveCustomFile: async (type, name, content) => {
    try {
      const success = await saveCustomFileApi(type, name, content);
      if (success) {
        showToast('File saved successfully', 'success');
        get().fetchCustomFiles();
        return true;
      }
      return false;
    } catch (err: any) {
      showToast(`Failed to save file: ${err.message}`, 'error');
      return false;
    }
  },

  deleteCustomFile: async (type, name) => {
    const confirmed = await confirmAction({
      title: 'Delete Custom File',
      message: `Are you sure you want to delete the custom file '${name}'? This action cannot be undone.`,
      confirmText: 'Delete File',
      danger: true
    });
    if (!confirmed) {
      return;
    }
    try {
      const success = await deleteCustomFileApi(type, name);
      if (success) {
        showToast('File deleted successfully', 'success');
        get().fetchCustomFiles();
      }
    } catch (err: any) {
      showToast(`Failed to delete file: ${err.message}`, 'error');
    }
  },

  fetchPolicies: async () => {
    try {
      const policies = await fetchPoliciesApi();
      set({ policies: Array.isArray(policies) ? policies : [] });
    } catch (err) {
      console.error('Failed to load policies:', err);
    }
  },

  savePolicy: async (policy) => {
    try {
      await savePolicyApi(policy);
      showToast('Policy saved successfully', 'success');
      set({ isPolicyModalOpen: false, editingPolicy: null });
      get().fetchPolicies();
    } catch (err: any) {
      showToast(`Failed to save policy: ${err.message}`, 'error');
    }
  },

  deletePolicy: async (id) => {
    const confirmed = await confirmAction({
      title: 'Delete Access Policy',
      message: 'Are you sure you want to delete this access policy?',
      confirmText: 'Delete Policy',
      danger: true
    });
    if (!confirmed) return;
    try {
      await deletePolicyApi(id);
      showToast('Policy deleted successfully', 'success');
      get().fetchPolicies();
    } catch (err: any) {
      showToast(`Failed to delete policy: ${err.message}`, 'error');
    }
  },

  fetchMappings: async () => {
    try {
      const mappings = await fetchMappingsApi();
      set({ mappings: Array.isArray(mappings) ? mappings : [] });
    } catch (err) {
      console.error('Failed to load mappings:', err);
    }
  },

  saveMapping: async (mapping) => {
    try {
      await saveMappingApi(mapping);
      showToast('Mapping saved successfully', 'success');
      set({ isMappingModalOpen: false, editingMapping: null });
      get().fetchMappings();
    } catch (err: any) {
      showToast(`Failed to save mapping: ${err.message}`, 'error');
    }
  },

  deleteMapping: async (id) => {
    const confirmed = await confirmAction({
      title: 'Delete Group Mapping',
      message: 'Are you sure you want to delete this group mapping?',
      confirmText: 'Delete Mapping',
      danger: true
    });
    if (!confirmed) return;
    try {
      await deleteMappingApi(id);
      showToast('Mapping deleted successfully', 'success');
      get().fetchMappings();
    } catch (err: any) {
      showToast(`Failed to delete mapping: ${err.message}`, 'error');
    }
  },

  openCustomFileModal: async (meta) => {
    if (meta) {
      const content = await get().fetchCustomFileContent(meta.type, meta.name);
      set({
        isCustomFileOpen: true,
        editingFileMeta: meta,
        editingFileContent: content,
        activeFileModalTab: 'editor'
      });
    } else {
      // Create new starter prompts JSON
      const defaultContent = JSON.stringify({
        description: "My custom prompt description",
        arguments: [
          { name: "topic", description: "Topic to write about", required: true }
        ],
        messages: [
          {
            role: "user",
            content: {
              type: "text",
              text: "Write a short summary about {{topic}}."
            }
          }
        ]
      }, null, 2);
      set({
        isCustomFileOpen: true,
        editingFileMeta: null,
        editingFileContent: defaultContent,
        activeFileModalTab: 'editor'
      });
    }
  },

  closeCustomFileModal: () => set({ isCustomFileOpen: false, editingFileMeta: null, editingFileContent: '' }),
  setEditingFileContent: (content) => set({ editingFileContent: content }),
  setActiveFileModalTab: (tab) => set({ activeFileModalTab: tab }),

  openPolicyModal: (policy) => set({ isPolicyModalOpen: true, editingPolicy: policy || null }),
  closePolicyModal: () => set({ isPolicyModalOpen: false, editingPolicy: null }),

  openMappingModal: (mapping) => set({ isMappingModalOpen: true, editingMapping: mapping || null }),
  closeMappingModal: () => set({ isMappingModalOpen: false, editingMapping: null })
}));
