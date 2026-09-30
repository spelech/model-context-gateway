import { create } from 'zustand';
import { showToast } from './useToastStore';
import { confirmAction } from './useConfirmStore';
import { RegisteredClient, NewClientResult } from '../shared/types';
import { fetchClientsApi, registerClientApi, deleteClientApi, cleanupClientsApi, rotateClientSecretApi } from '../api/clientApi';

export type { RegisteredClient, NewClientResult };

interface ClientStore {
  clients: RegisteredClient[];
  isLoadingClients: boolean;
  isAddClientOpen: boolean;
  createdClientResult: NewClientResult | null;

  fetchClients: () => Promise<void>;
  registerClient: (
    displayName: string,
    scopes: string[],
    redirectUris?: string[],
    grantTypes?: string[],
    clientType?: 'confidential' | 'public',
    expiresInDays?: number
  ) => Promise<void>;
  deleteClient: (id: string, name: string) => Promise<void>;
  cleanupClients: (retentionDays?: number) => Promise<void>;
  rotateClientSecret: (id: string) => Promise<{ clientId: string; clientSecret: string }>;

  openAddClientModal: () => void;
  closeClientModal: () => void;
}

export const useClientStore = create<ClientStore>((set, get) => ({
  clients: [],
  isLoadingClients: false,
  isAddClientOpen: false,
  createdClientResult: null,

  fetchClients: async () => {
    set({ isLoadingClients: true });
    try {
      const data = await fetchClientsApi();
      set({ clients: data || [], isLoadingClients: false });
    } catch (e: any) {
      console.error('Error fetching clients:', e);
      set({ isLoadingClients: false });
    }
  },

  registerClient: async (displayName, scopes, redirectUris, grantTypes, clientType, expiresInDays) => {
    try {
      const result = await registerClientApi(displayName, scopes, redirectUris, grantTypes, clientType, expiresInDays);
      set({ createdClientResult: result });
      showToast('Client registered successfully', 'success');
      get().fetchClients();
    } catch (e: any) {
      showToast(`Error registering client: ${e.message}`, 'error');
      throw e;
    }
  },

  deleteClient: async (id, name) => {
    const confirmed = await confirmAction({
      title: 'Delete Client',
      message: `Are you sure you want to delete the registered client '${name}'?`,
      confirmText: 'Delete Client',
      danger: true
    });
    if (!confirmed) return;
    try {
      await deleteClientApi(id);
      showToast('Client deleted successfully', 'success');
      get().fetchClients();
    } catch (e: any) {
      showToast(`Error deleting client: ${e.message}`, 'error');
    }
  },

  cleanupClients: async (retentionDays = 30) => {
    const confirmed = await confirmAction({
      title: 'Clean Up DCR Clients',
      message: 'Prune duplicate and expired dynamic client registrations (RFC 7591) while preserving active configurations?',
      confirmText: 'Clean Up',
      danger: false
    });
    if (!confirmed) return;
    try {
      const res = await cleanupClientsApi(retentionDays);
      showToast(res.cleanedCount > 0 ? `Cleaned up ${res.cleanedCount} stale / duplicate DCR registrations` : 'All dynamic client registrations are clean', 'success');
      get().fetchClients();
    } catch (e: any) {
      showToast(`Error cleaning up clients: ${e.message}`, 'error');
    }
  },

  rotateClientSecret: async (id: string) => {
    try {
      const result = await rotateClientSecretApi(id);
      set((state) => ({
        clients: state.clients.map((c) =>
          c.id === id || c.clientId === id
            ? { ...c, lastUsedAt: null, LastUsedAt: null }
            : c
        )
      }));
      showToast('Client secret rotated successfully', 'success');
      return result;
    } catch (e: any) {
      showToast(`Error rotating client secret: ${e.message}`, 'error');
      throw e;
    }
  },

  openAddClientModal: () => set({ isAddClientOpen: true, createdClientResult: null }),
  closeClientModal: () => set({ isAddClientOpen: false, createdClientResult: null })
}));
