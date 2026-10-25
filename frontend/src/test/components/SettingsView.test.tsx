/** @requirement UI-113 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { SettingsView } from '../../components/settings/SettingsView';
import { useSettingsStore } from '../../stores/useSettingsStore';
import { mockApiResponse } from '../setup';

describe('SettingsView component', () => {
  const testSettings = {
    embeddingProvider: 'local',
    embeddingModelDir: 'data/models',
    embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
    embeddingApiModel: 'all-MiniLM-L6-v2',
    embeddingApiKey: '',
    requireManualApproval: false
  };

  const testAuthProviders = [
    {
      providerName: 'ActiveDirectory',
      displayName: 'Active Directory',
      isEnabled: false
    },
    {
      providerName: 'HeaderAuth',
      displayName: 'OIDC / Reverse Proxy Headers',
      isEnabled: true,
      userHeader: 'Remote-User',
      groupsHeader: 'Remote-Groups'
    }
  ];

  const testSecretProviders = [
    {
      providerName: 'Vault',
      displayName: 'HashiCorp Vault (KV v2)',
      isEnabled: true,
      configJson: JSON.stringify({
        address: 'http://vault:8200',
        token: 's.mySecretVaultToken',
        mountPath: 'secret/data/'
      })
    },
    {
      providerName: 'WindowsRegistry',
      displayName: 'Windows Registry (DPAPI)',
      isEnabled: false,
      configJson: JSON.stringify({
        keyPath: 'HKCU\\Software\\McpRouter\\Secrets'
      })
    },
    {
      providerName: 'Environment',
      displayName: 'Container Environment',
      isEnabled: true,
      configJson: JSON.stringify({
        prefix: 'MCP_SECRET_'
      })
    }
  ];

  const testCustomFiles = [
    {
      type: 'prompts' as const,
      name: 'general-assistant.json',
      sizeBytes: 2048,
      lastModified: '2026-08-14T00:00:00Z'
    }
  ];

  const testPolicies = [
    {
      id: 'pol-1',
      targetId: 'server:ha',
      requiredGroup: 'house_member',
      isAllowed: true
    }
  ];

  const testMappings = [
    {
      id: 'map-1',
      externalId: 'oidc_admins',
      internalGroup: 'Administrators'
    }
  ];

  beforeEach(() => {
    mockApiResponse('/api/settings', testSettings);
    mockApiResponse('/api/providers/auth', testAuthProviders);
    mockApiResponse('/api/providers/secrets', testSecretProviders);
    mockApiResponse('/api/custom-files', testCustomFiles);
    mockApiResponse('/api/permissions/policies', testPolicies);
    mockApiResponse('/api/permissions/mappings', testMappings);
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type Positive
   * @description renders tab navigation and switches active subviews
   */
  it('renders tab navigation and switches active subviews', async () => {
    await act(async () => {
      render(<SettingsView />);
    });

    expect(screen.getByRole('button', { name: /vector & search/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /identity & auth/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /secret providers/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /prompts & resources/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /access control/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /code mode/i })).toBeInTheDocument();

    // Default active subview is search
    expect(screen.getByRole('heading', { name: /general settings/i })).toBeInTheDocument();

    // Switch to Identity subview
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /identity & auth/i }));
    });
    expect(screen.getByRole('heading', { name: /identity & auth providers/i })).toBeInTheDocument();

    // Switch to Secrets subview
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /secret providers/i }));
    });
    expect(screen.getByRole('heading', { name: /secret providers/i })).toBeInTheDocument();

    // Switch to Prompts & Resources subview
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /prompts & resources/i }));
    });
    expect(screen.getByRole('heading', { name: /prompts & resources file manager/i })).toBeInTheDocument();

    // Switch to Access Control subview
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /access control/i }));
    });
    expect(screen.getByRole('heading', { name: /access control policies/i })).toBeInTheDocument();

    // Switch to Code Mode subview
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /code mode/i }));
    });
    expect(screen.getByRole('heading', { name: /code mode/i })).toBeInTheDocument();
  });

  describe('Vector & Search Subview', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description saves embedding settings and displays success feedback
     */
    it('saves embedding settings and displays success feedback', async () => {
      let postedSettings: any = null;
      mockApiResponse('/api/settings', (_url, options) => {
        if (options?.method === 'POST') {
          postedSettings = JSON.parse(options.body as string);
          return { success: true };
        }
        return testSettings;
      });

      await act(async () => {
        render(<SettingsView />);
      });

      const providerSelect = screen.getByLabelText('Embedding Provider');
      fireEvent.change(providerSelect, { target: { value: 'api' } });

      const urlInput = screen.getByLabelText('Embedding API URL');
      fireEvent.change(urlInput, { target: { value: 'http://custom-embeddings:8000/v1' } });

      const saveBtn = screen.getByRole('button', { name: /save settings/i });
      await act(async () => {
        fireEvent.click(saveBtn);
      });

      expect(postedSettings).toMatchObject({
        embeddingProvider: 'api',
        embeddingApiUrl: 'http://custom-embeddings:8000/v1'
      });
    });
  });

  describe('Identity & Auth Subview', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description saves Auth Provider configurations including Active Directory and OIDC header mappings
     */
    it('saves Auth Provider configurations including Active Directory and OIDC header mappings', async () => {
      const savedProviders: any[] = [];
      mockApiResponse('/api/providers/auth', (_url, options) => {
        if (options?.method === 'POST') {
          savedProviders.push(JSON.parse(options.body as string));
          return { success: true };
        }
        return testAuthProviders;
      });

      await act(async () => {
        render(<SettingsView />);
      });

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /identity & auth/i }));
      });

      const adCheckbox = document.getElementById('auth-ad-enabled') as HTMLInputElement;
      expect(adCheckbox).not.toBeChecked();
      fireEvent.click(adCheckbox);

      const userHeaderInput = document.getElementById('auth-user-header') as HTMLInputElement;
      fireEvent.change(userHeaderInput, { target: { value: 'X-Forwarded-User' } });

      const saveBtn = screen.getByRole('button', { name: /save auth config/i });
      await act(async () => {
        fireEvent.click(saveBtn);
      });

      expect(savedProviders).toContainEqual(
        expect.objectContaining({
          providerName: 'ActiveDirectory',
          displayName: 'Active Directory',
          isEnabled: true
        })
      );

      expect(savedProviders).toContainEqual(
        expect.objectContaining({
          providerName: 'HeaderAuth',
          displayName: 'OIDC / Reverse Proxy Headers',
          userHeader: 'X-Forwarded-User',
          groupsHeader: 'Remote-Groups',
          isEnabled: true
        })
      );
    });
  });

  describe('Secret Providers Subview', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description saves secret providers while preserving Vault config and secrets
     */
    it('saves secret providers while preserving Vault config and secrets', async () => {
      const savedSecrets: any[] = [];
      mockApiResponse('/api/providers/secrets', (_url, options) => {
        if (options?.method === 'POST') {
          savedSecrets.push(JSON.parse(options.body as string));
          return { success: true };
        }
        return testSecretProviders;
      });

      await act(async () => {
        render(<SettingsView />);
      });

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /secret providers/i }));
      });

      expect(screen.getByPlaceholderText('http://vault:8200')).toHaveValue('http://vault:8200');
      expect(screen.getByPlaceholderText('Vault Token (optional)')).toHaveValue('s.mySecretVaultToken');
      expect(screen.getByPlaceholderText('Mount Path (secret/data/)')).toHaveValue('secret/data/');

      const saveBtn = screen.getByRole('button', { name: /save secret config/i });
      await act(async () => {
        fireEvent.click(saveBtn);
      });

      expect(savedSecrets).toContainEqual(
        expect.objectContaining({
          providerName: 'Vault',
          displayName: 'HashiCorp Vault (KV v2)',
          configJson: JSON.stringify({
            address: 'http://vault:8200',
            token: 's.mySecretVaultToken',
            mountPath: 'secret/data/'
          }),
          isEnabled: true
        })
      );

      expect(savedSecrets).toContainEqual(
        expect.objectContaining({
          providerName: 'WindowsRegistry',
          displayName: 'Windows Registry (DPAPI)',
          configJson: JSON.stringify({
            keyPath: 'HKCU\\Software\\McpRouter\\Secrets'
          }),
          isEnabled: false
        })
      );

      expect(savedSecrets).toContainEqual(
        expect.objectContaining({
          providerName: 'Environment',
          displayName: 'Container Environment',
          configJson: JSON.stringify({
            prefix: 'MCP_SECRET_'
          }),
          isEnabled: true
        })
      );
    });
  });

  describe('Prompts & Resources Subview', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description renders custom files table with edit and delete actions
     */
    it('renders custom files table with edit and delete actions', async () => {
      const openModalSpy = vi.fn();
      const deleteSpy = vi.fn();
      useSettingsStore.setState({
        openCustomFileModal: openModalSpy,
        deleteCustomFile: deleteSpy
      });

      await act(async () => {
        render(<SettingsView />);
      });

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /prompts & resources/i }));
      });

      expect(screen.getByText('general-assistant.json')).toBeInTheDocument();
      expect(screen.getByText('2.00 KB')).toBeInTheDocument();

      // Create file button
      const createBtn = screen.getByRole('button', { name: /create file/i });
      fireEvent.click(createBtn);
      expect(openModalSpy).toHaveBeenCalled();

      // Edit file button
      const editBtn = screen.getByRole('button', { name: /edit/i });
      fireEvent.click(editBtn);
      expect(openModalSpy).toHaveBeenCalledWith(expect.objectContaining({ name: 'general-assistant.json' }));

      // Delete file button
      const deleteBtn = screen.getByRole('button', { name: /delete/i });
      fireEvent.click(deleteBtn);
      expect(deleteSpy).toHaveBeenCalledWith('prompts', 'general-assistant.json');
    });
  });

  describe('Access Control Subview', () => {
    /**
     * @requirement UI-01
     * @category UI
     * @type Positive
     * @description renders access policies and group mappings with CRUD actions
     */
    it('renders access policies and group mappings with CRUD actions', async () => {
      const openPolicySpy = vi.fn();
      const deletePolicySpy = vi.fn();
      const openMappingSpy = vi.fn();
      const deleteMappingSpy = vi.fn();

      useSettingsStore.setState({
        openPolicyModal: openPolicySpy,
        deletePolicy: deletePolicySpy,
        openMappingModal: openMappingSpy,
        deleteMapping: deleteMappingSpy
      });

      await act(async () => {
        render(<SettingsView />);
      });

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /access control/i }));
      });

      // Policies table
      expect(screen.getByText('server:ha')).toBeInTheDocument();
      expect(screen.getByText('house_member')).toBeInTheDocument();
      expect(screen.getByText('ALLOW')).toBeInTheDocument();

      const createPolicyBtn = screen.getByRole('button', { name: /create policy/i });
      fireEvent.click(createPolicyBtn);
      expect(openPolicySpy).toHaveBeenCalled();

      // Mappings table
      expect(screen.getByText('oidc_admins')).toBeInTheDocument();
      expect(screen.getByText('Administrators')).toBeInTheDocument();

      const createMappingBtn = screen.getByRole('button', { name: /create mapping/i });
      fireEvent.click(createMappingBtn);
      expect(openMappingSpy).toHaveBeenCalled();
    });
  });
});
