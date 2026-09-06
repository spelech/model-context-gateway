import { describe, it, expect } from 'vitest';
import { useSettingsStore, AccessPolicy, GroupMapping } from '../../stores/useSettingsStore';
import { useToastStore } from '../../stores/useToastStore';
import { useConfirmStore } from '../../stores/useConfirmStore';
import { mockApiResponse } from '../setup';

describe('usePolicyStore (useSettingsStore policy & mapping actions)', () => {
  const samplePolicy: AccessPolicy = {
    id: 'pol-1',
    targetId: 'server:ha',
    requiredGroup: 'smarthome_admins',
    isAllowed: true
  };

  const sampleMapping: GroupMapping = {
    id: 'map-1',
    externalId: 'S-1-5-21-999-500',
    internalGroup: 'Administrators'
  };

  /**
   * @requirement AUTH-STORE-POLICY-INIT
   * @category AUTH
   * @type Positive
   * @description initializes with empty policies and mappings
   */
  it('initializes with empty policies and mappings', () => {
    const state = useSettingsStore.getState();
    expect(state.policies).toEqual([]);
    expect(state.mappings).toEqual([]);
    expect(state.isPolicyModalOpen).toBe(false);
    expect(state.editingPolicy).toBeNull();
    expect(state.isMappingModalOpen).toBe(false);
    expect(state.editingMapping).toBeNull();
  });

  describe('policies management', () => {
    /**
     * @requirement AUTH-STORE-POLICY-FETCH
     * @category AUTH
     * @type Positive
     * @description fetches access policies and updates store
     */
    it('fetches access policies and updates store', async () => {
      mockApiResponse('/api/permissions/policies', [samplePolicy]);

      await useSettingsStore.getState().fetchPolicies();

      expect(useSettingsStore.getState().policies).toHaveLength(1);
      expect(useSettingsStore.getState().policies[0].targetId).toBe('server:ha');
    });

    /**
     * @requirement AUTH-STORE-POLICY-CREATE
     * @category AUTH
     * @type Positive
     * @description creates/saves a policy (ALLOW rule) and closes modal
     */
    it('creates/saves a policy (ALLOW rule) and closes modal', async () => {
      let postBody: any = null;
      mockApiResponse('/api/permissions/policies', (_url, options) => {
        if (options?.method === 'POST') {
          postBody = JSON.parse(options.body as string);
          return { id: 'new-pol-id', ...postBody };
        }
        return [samplePolicy];
      });

      useSettingsStore.setState({ isPolicyModalOpen: true });

      await useSettingsStore.getState().savePolicy({
        targetId: 'tool:docker__delete_container',
        requiredGroup: 'full_admin',
        isAllowed: false // DENY rule
      });

      expect(postBody).toEqual({
        targetId: 'tool:docker__delete_container',
        requiredGroup: 'full_admin',
        isAllowed: false
      });
      expect(useSettingsStore.getState().isPolicyModalOpen).toBe(false);
      expect(useToastStore.getState().toasts.some((t) => t.message.includes('Policy saved successfully'))).toBe(true);
    });

    /**
     * @requirement GUARD-01
     * @category GUARD
     * @type Negative
     * @description handles policy save failure with error toast
     */
    it('handles policy save failure with error toast', async () => {
      mockApiResponse('/api/permissions/policies', 'Save policy failed', 500);

      await useSettingsStore.getState().savePolicy(samplePolicy);

      expect(useToastStore.getState().toasts.some((t) => t.type === 'error')).toBe(true);
    });

    /**
     * @requirement UI-CONFIRM-MODAL
     * @category UI
     * @type PositiveFeature
     * @description Deletes an access policy when confirmed via confirm modal.
     */
    it('deletes a policy when confirmed', async () => {
      let deleteCalled = false;
      mockApiResponse(/\/api\/permissions\/policies\/pol-1/, (_url, options) => {
        if (options?.method === 'DELETE') {
          deleteCalled = true;
          return { success: true };
        }
        return { success: true };
      });

      const deletePromise = useSettingsStore.getState().deletePolicy('pol-1');

      expect(useConfirmStore.getState().isOpen).toBe(true);
      expect(useConfirmStore.getState().options.title).toBe('Delete Access Policy');
      expect(useConfirmStore.getState().options.danger).toBe(true);

      useConfirmStore.getState().handleConfirm();
      await deletePromise;

      expect(deleteCalled).toBe(true);
      expect(useToastStore.getState().toasts.some((t) => t.message.includes('Policy deleted successfully'))).toBe(true);
    });

    /**
     * @requirement UI-CONFIRM-MODAL
     * @category UI
     * @type FailClosedGuardrail
     * @description Does not delete policy when confirm is cancelled.
     */
    it('does not delete policy when confirm is cancelled', async () => {
      let deleteCalled = false;
      mockApiResponse(/\/api\/permissions\/policies\/pol-1/, (_url, options) => {
        if (options?.method === 'DELETE') {
          deleteCalled = true;
          return { success: true };
        }
        return { success: true };
      });

      const deletePromise = useSettingsStore.getState().deletePolicy('pol-1');

      expect(useConfirmStore.getState().isOpen).toBe(true);
      useConfirmStore.getState().handleCancel();
      await deletePromise;

      expect(deleteCalled).toBe(false);
    });
  });

  describe('mappings management', () => {
    /**
     * @requirement AUTH-STORE-MAPPING-FETCH
     * @category AUTH
     * @type Positive
     * @description fetches group mappings and updates store
     */
    it('fetches group mappings and updates store', async () => {
      mockApiResponse('/api/permissions/mappings', [sampleMapping]);

      await useSettingsStore.getState().fetchMappings();

      expect(useSettingsStore.getState().mappings).toHaveLength(1);
      expect(useSettingsStore.getState().mappings[0].internalGroup).toBe('Administrators');
    });

    /**
     * @requirement AUTH-STORE-MAPPING-SAVE
     * @category AUTH
     * @type Positive
     * @description saves a group mapping and closes mapping modal
     */
    it('saves a group mapping and closes mapping modal', async () => {
      let postBody: any = null;
      mockApiResponse('/api/permissions/mappings', (_url, options) => {
        if (options?.method === 'POST') {
          postBody = JSON.parse(options.body as string);
          return { id: 'new-map-id', ...postBody };
        }
        return [sampleMapping];
      });

      useSettingsStore.setState({ isMappingModalOpen: true });

      await useSettingsStore.getState().saveMapping({
        externalId: 'oidc_ops',
        internalGroup: 'devops_team'
      });

      expect(postBody).toEqual({
        externalId: 'oidc_ops',
        internalGroup: 'devops_team'
      });
      expect(useSettingsStore.getState().isMappingModalOpen).toBe(false);
      expect(useToastStore.getState().toasts.some((t) => t.message.includes('Mapping saved successfully'))).toBe(true);
    });

    /**
     * @requirement UI-CONFIRM-MODAL
     * @category UI
     * @type PositiveFeature
     * @description Deletes a group mapping when confirmed via confirm modal.
     */
    it('deletes a group mapping when confirmed', async () => {
      let deleteCalled = false;
      mockApiResponse(/\/api\/permissions\/mappings\/map-1/, (_url, options) => {
        if (options?.method === 'DELETE') {
          deleteCalled = true;
          return { success: true };
        }
        return { success: true };
      });

      const deletePromise = useSettingsStore.getState().deleteMapping('map-1');

      expect(useConfirmStore.getState().isOpen).toBe(true);
      expect(useConfirmStore.getState().options.title).toBe('Delete Group Mapping');
      expect(useConfirmStore.getState().options.danger).toBe(true);

      useConfirmStore.getState().handleConfirm();
      await deletePromise;

      expect(deleteCalled).toBe(true);
      expect(useToastStore.getState().toasts.some((t) => t.message.includes('Mapping deleted successfully'))).toBe(true);
    });

    /**
     * @requirement UI-CONFIRM-MODAL
     * @category UI
     * @type FailClosedGuardrail
     * @description Does not delete group mapping when confirm is cancelled.
     */
    it('does not delete group mapping when confirm is cancelled', async () => {
      let deleteCalled = false;
      mockApiResponse(/\/api\/permissions\/mappings\/map-1/, (_url, options) => {
        if (options?.method === 'DELETE') {
          deleteCalled = true;
          return { success: true };
        }
        return { success: true };
      });

      const deletePromise = useSettingsStore.getState().deleteMapping('map-1');

      expect(useConfirmStore.getState().isOpen).toBe(true);
      useConfirmStore.getState().handleCancel();
      await deletePromise;

      expect(deleteCalled).toBe(false);
    });
  });

  describe('customFiles management', () => {
    /**
     * @requirement UI-CONFIRM-MODAL
     * @category UI
     * @type PositiveFeature
     * @description Deletes a custom file when confirmed via confirm modal.
     */
    it('deletes a custom file when confirmed', async () => {
      let deleteCalled = false;
      mockApiResponse(/\/api\/custom-files\/prompts\/test\.json/, (_url, options) => {
        if (options?.method === 'DELETE') {
          deleteCalled = true;
          return { success: true };
        }
        return { success: true };
      });

      const deletePromise = useSettingsStore.getState().deleteCustomFile('prompts', 'test.json');

      expect(useConfirmStore.getState().isOpen).toBe(true);
      expect(useConfirmStore.getState().options.title).toBe('Delete Custom File');
      expect(useConfirmStore.getState().options.danger).toBe(true);

      useConfirmStore.getState().handleConfirm();
      await deletePromise;

      expect(deleteCalled).toBe(true);
      expect(useToastStore.getState().toasts.some((t) => t.message.includes('File deleted successfully'))).toBe(true);
    });

    /**
     * @requirement UI-CONFIRM-MODAL
     * @category UI
     * @type FailClosedGuardrail
     * @description Does not delete custom file when confirm is cancelled.
     */
    it('does not delete custom file when confirm is cancelled', async () => {
      let deleteCalled = false;
      mockApiResponse(/\/api\/custom-files\/prompts\/test\.json/, (_url, options) => {
        if (options?.method === 'DELETE') {
          deleteCalled = true;
          return { success: true };
        }
        return { success: true };
      });

      const deletePromise = useSettingsStore.getState().deleteCustomFile('prompts', 'test.json');

      expect(useConfirmStore.getState().isOpen).toBe(true);
      useConfirmStore.getState().handleCancel();
      await deletePromise;

      expect(deleteCalled).toBe(false);
    });
  });

  describe('modals state', () => {
    /**
     * @requirement AUTH-STORE-POLICY-MODAL-TOGGLE
     * @category AUTH
     * @type Positive
     * @description handles policy modal open and close
     */
    it('handles policy modal open and close', () => {
      useSettingsStore.getState().openPolicyModal(samplePolicy);
      expect(useSettingsStore.getState().isPolicyModalOpen).toBe(true);
      expect(useSettingsStore.getState().editingPolicy).toEqual(samplePolicy);

      useSettingsStore.getState().closePolicyModal();
      expect(useSettingsStore.getState().isPolicyModalOpen).toBe(false);
      expect(useSettingsStore.getState().editingPolicy).toBeNull();
    });

    /**
     * @requirement AUTH-STORE-MAPPING-MODAL-TOGGLE
     * @category AUTH
     * @type Positive
     * @description handles mapping modal open and close
     */
    it('handles mapping modal open and close', () => {
      useSettingsStore.getState().openMappingModal(sampleMapping);
      expect(useSettingsStore.getState().isMappingModalOpen).toBe(true);
      expect(useSettingsStore.getState().editingMapping).toEqual(sampleMapping);

      useSettingsStore.getState().closeMappingModal();
      expect(useSettingsStore.getState().isMappingModalOpen).toBe(false);
      expect(useSettingsStore.getState().editingMapping).toBeNull();
    });
  });
});
