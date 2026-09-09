import { create } from 'zustand';
import { getCurrentUser, getHealth } from '../api/userApi';
import { UserInfo } from '../types';

export interface UserStore {
  user: UserInfo | null;
  version: string;
  service: string;
  isLoadingUser: boolean;
  loadUser: () => Promise<void>;
  loadVersion: () => Promise<void>;
}

export const useUserStore = create<UserStore>((set) => ({
  user: null,
  version: '5.11.1', // fallback default
  service: 'ModelContextGateway',
  isLoadingUser: false,
  loadUser: async () => {
    set({ isLoadingUser: true });
    try {
      const data = await getCurrentUser();
      set({ user: data, isLoadingUser: false });
    } catch (e) {
      console.error('Error loading user profile:', e);
      set({ user: { authenticated: false }, isLoadingUser: false });
    }
  },
  loadVersion: async () => {
    try {
      const data = await getHealth();
      if (data) {
        if (data.version) {
          set({ version: data.version });
        }
        if (data.service) {
          set({ service: data.service });
        }
      }
    } catch (e) {
      console.error('Error loading version:', e);
    }
  }
}));
