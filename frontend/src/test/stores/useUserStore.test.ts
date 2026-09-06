/** @requirement UI-101 */

import { describe, it, expect, vi } from 'vitest';
import { useUserStore } from '../../stores/useUserStore';
import { mockApiResponse } from '../setup';

describe('useUserStore', () => {
  /**
   * @requirement UI-USER-STORE-INIT
   * @category AUTH
   * @type Positive
   * @description should initialize with default values
   */
  it('should initialize with default values', () => {
    const state = useUserStore.getState();
    expect(state.user).toBeNull();
    expect(state.version).toBe('5.2.0');
    expect(state.service).toBe('ModelContextGateway');
    expect(state.isLoadingUser).toBe(false);
  });

  describe('loadUser', () => {
    /**
     * @requirement UI-USER-STORE-LOAD-PROFILE
     * @category AUTH
     * @type Positive
     * @description successfully loads user profile from /api/me
     */
    it('successfully loads user profile from /api/me', async () => {
      const mockUser = {
        authenticated: true,
        username: 'admin',
        name: 'Admin User',
        email: 'admin@example.com',
        groups: ['full_admin', 'engineering']
      };
      mockApiResponse('/api/me', mockUser);

      const promise = useUserStore.getState().loadUser();
      expect(useUserStore.getState().isLoadingUser).toBe(true);

      await promise;

      const state = useUserStore.getState();
      expect(state.isLoadingUser).toBe(false);
      expect(state.user).toEqual(mockUser);
      expect(state.user?.groups).toContain('full_admin');
    });

    /**
     * @requirement UI-USER-STORE-ERROR-FALLBACK
     * @category AUTH
     * @type Positive
     * @description handles error response gracefully and sets unauthenticated user state
     */
    it('handles error response gracefully and sets unauthenticated user state', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      mockApiResponse('/api/me', 'Unauthorized', 401, 'Unauthorized');

      await useUserStore.getState().loadUser();

      const state = useUserStore.getState();
      expect(state.isLoadingUser).toBe(false);
      expect(state.user).toEqual({ authenticated: false });
      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    /**
     * @requirement UI-USER-STORE-NETWORK-FAILURE
     * @category AUTH
     * @type Positive
     * @description handles network failure gracefully
     */
    it('handles network failure gracefully', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      mockApiResponse('/api/me', () => {
        throw new Error('Network failure');
      }, 500);

      await useUserStore.getState().loadUser();

      const state = useUserStore.getState();
      expect(state.isLoadingUser).toBe(false);
      expect(state.user).toEqual({ authenticated: false });
      consoleSpy.mockRestore();
    });

    /**
     * @requirement UI-USER-STORE-ROLE-EXTRACTION
     * @category AUTH
     * @type Positive
     * @description correctly handles non-admin user role extraction
     */
    it('correctly handles non-admin user role extraction', async () => {
      const standardUser = {
        authenticated: true,
        username: 'guest',
        name: 'Guest User',
        groups: ['house_member']
      };
      mockApiResponse('/api/me', standardUser);

      await useUserStore.getState().loadUser();

      const state = useUserStore.getState();
      expect(state.user?.groups).not.toContain('full_admin');
      expect(state.user?.groups).toContain('house_member');
    });
  });

  describe('loadVersion', () => {
    /**
     * @requirement UI-USER-STORE-HEALTH-VERSION
     * @category AUTH
     * @type Positive
     * @description successfully updates version and service from /health endpoint
     */
    it('successfully updates version and service from /health endpoint', async () => {
      mockApiResponse('/health', { version: '5.2.0', service: 'ModelContextGateway', status: 'healthy' });

      await useUserStore.getState().loadVersion();

      expect(useUserStore.getState().version).toBe('5.2.0');
      expect(useUserStore.getState().service).toBe('ModelContextGateway');
    });

    /**
     * @requirement UI-USER-STORE-VERSION-FALLBACK
     * @category AUTH
     * @type Positive
     * @description keeps existing fallback version on error
     */
    it('keeps existing fallback version on error', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      useUserStore.setState({ version: '5.0.7', service: 'ModelContextGateway' });
      mockApiResponse('/health', 'Service Unavailable', 503, 'Service Unavailable');

      await useUserStore.getState().loadVersion();

      expect(useUserStore.getState().version).toBe('5.0.7');
      expect(useUserStore.getState().service).toBe('ModelContextGateway');
      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });
  });
});
