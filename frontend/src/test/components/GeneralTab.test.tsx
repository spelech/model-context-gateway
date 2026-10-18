import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { GeneralTab } from '../../components/settings/GeneralTab';

describe('GeneralTab component', () => {
  /**
   * @requirement AUTH-PERSONAL-APPKEY-QUOTA-OVERRIDE
   * @category AUTH
   * @type PositiveFeature
   * @description Renders GeneralTab with security default quota inputs and saves settings.
   */
  it('renders GeneralTab with security default quota inputs and triggers save', async () => {
    const saveSpy = vi.fn().mockResolvedValue(true);
    render(
      <GeneralTab
        settings={{
          dashboardTitle: 'Custom MCP Gateway',
          dashboardIcon: 'fa-solid fa-server',
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          allowOpenClientRegistration: false,
          userMaxKeys: 5,
          globalMaxKeys: 100,
        }}
        saveEmbeddingSettings={saveSpy}
      />
    );

    expect(screen.getByText('General Settings')).toBeInTheDocument();
    expect(screen.getByText('Security Defaults')).toBeInTheDocument();

    const userQuotaInput = screen.getByLabelText(/Default User Quota \(UserMaxKeys\)/i);
    const globalQuotaInput = screen.getByLabelText(/Global Max Keys/i);
    const dcrCheckbox = screen.getByRole('checkbox');

    expect(userQuotaInput).toHaveValue(5);
    expect(globalQuotaInput).toHaveValue(100);
    expect(dcrCheckbox).not.toBeChecked();

    // Modify user and global quotas
    fireEvent.change(userQuotaInput, { target: { value: '10' } });
    fireEvent.change(globalQuotaInput, { target: { value: '250' } });
    fireEvent.click(dcrCheckbox);

    expect(userQuotaInput).toHaveValue(10);
    expect(globalQuotaInput).toHaveValue(250);
    expect(dcrCheckbox).toBeChecked();

    const saveBtn = screen.getByRole('button', { name: /save settings/i });
    await act(async () => {
      fireEvent.click(saveBtn);
    });

    expect(saveSpy).toHaveBeenCalledWith({
      dashboardTitle: 'Custom MCP Gateway',
      dashboardIcon: 'fa-solid fa-server',
      embeddingProvider: 'local',
      embeddingModelDir: 'data/models',
      embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
      embeddingApiModel: 'all-MiniLM-L6-v2',
      embeddingApiKey: '',
      allowOpenClientRegistration: true,
      userMaxKeys: 10,
      globalMaxKeys: 250,
      toolSearchDefaultLimit: 5,
      toolSearchMinScore: 0.35,
      toolSearchScoreDropoffRatio: 0.6,
    });
  });

  /**
   * @requirement MCP-SEARCH-TOOL-LIMITS-AND-THRESHOLDS
   * @category MCP
   * @type PositiveFeature
   * @description Renders GeneralTab with configurable tool search limits and thresholds and triggers save.
   */
  it('renders GeneralTab with tool search limit and threshold inputs and triggers save', async () => {
    const saveSpy = vi.fn().mockResolvedValue(true);
    render(
      <GeneralTab
        settings={{
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          toolSearchDefaultLimit: 8,
          toolSearchMinScore: 0.40,
          toolSearchScoreDropoffRatio: 0.70,
        }}
        saveEmbeddingSettings={saveSpy}
      />
    );

    const limitInput = screen.getByLabelText(/Default Tool Search Limit/i);
    const minScoreInput = screen.getByLabelText(/Min Score Threshold/i);
    const dropoffInput = screen.getByLabelText(/Relative Score Dropoff Ratio/i);

    expect(limitInput).toHaveValue(8);
    expect(minScoreInput).toHaveValue(0.4);
    expect(dropoffInput).toHaveValue(0.7);

    // Modify values
    fireEvent.change(limitInput, { target: { value: '12' } });
    fireEvent.change(minScoreInput, { target: { value: '0.45' } });
    fireEvent.change(dropoffInput, { target: { value: '0.75' } });

    expect(limitInput).toHaveValue(12);
    expect(minScoreInput).toHaveValue(0.45);
    expect(dropoffInput).toHaveValue(0.75);

    const saveBtn = screen.getByRole('button', { name: /save settings/i });
    await act(async () => {
      fireEvent.click(saveBtn);
    });

    expect(saveSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        toolSearchDefaultLimit: 12,
        toolSearchMinScore: 0.45,
        toolSearchScoreDropoffRatio: 0.75,
      })
    );
  });

  /**
   * @requirement AUTH-PERSONAL-APPKEY-QUOTA-OVERRIDE
   * @category AUTH
   * @type PositiveFeature
   * @description Updates form state when settings prop changes.
   */
  it('updates form state when settings prop changes', async () => {
    const saveSpy = vi.fn().mockResolvedValue(true);
    const { rerender } = render(
      <GeneralTab
        settings={{
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          userMaxKeys: 5,
          globalMaxKeys: 100,
        }}
        saveEmbeddingSettings={saveSpy}
      />
    );

    expect(screen.getByLabelText(/Default User Quota \(UserMaxKeys\)/i)).toHaveValue(5);

    rerender(
      <GeneralTab
        settings={{
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          userMaxKeys: 20,
          globalMaxKeys: 500,
        }}
        saveEmbeddingSettings={saveSpy}
      />
    );

    expect(screen.getByLabelText(/Default User Quota \(UserMaxKeys\)/i)).toHaveValue(20);
    expect(screen.getByLabelText(/Global Max Keys/i)).toHaveValue(500);
  });

  /**
   * @requirement SEC-MASTERKEY-UI-STATUS-BANNER
   * @category SEC
   * @type PositiveFeature
   * @description Displays warning banner when keySource is AutoGenerated and opens custom master key modal.
   */
  it('renders AutoGenerated warning banner and opens MasterKeyModal', async () => {
    const saveSpy = vi.fn().mockResolvedValue(true);
    const setKeySpy = vi.fn().mockResolvedValue({ success: true, message: 'Key updated' });

    render(
      <GeneralTab
        settings={{
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          masterKeySource: 'AutoGenerated',
        }}
        saveEmbeddingSettings={saveSpy}
        setMasterKey={setKeySpy}
      />
    );

    expect(screen.getByText(/Auto-Generated Master Key in Use/i)).toBeInTheDocument();
    const setKeyBtn = screen.getByRole('button', { name: /Set Custom Master Key/i });
    expect(setKeyBtn).toBeInTheDocument();

    fireEvent.click(setKeyBtn);
    expect(screen.getByText('Set Master Encryption Key')).toBeInTheDocument();
    expect(screen.getByLabelText(/New Master Key/i)).toBeInTheDocument();
  });

  /**
   * @requirement SEC-MASTERKEY-EXTERNAL-LOCKED-BADGE
   * @category SEC
   * @type PositiveFeature
   * @description Displays locked badge when master key is externally managed via Vault or Environment.
   */
  it('renders locked badge when master key is managed externally', async () => {
    const saveSpy = vi.fn().mockResolvedValue(true);

    const { rerender } = render(
      <GeneralTab
        settings={{
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          masterKeySource: 'External',
        }}
        saveEmbeddingSettings={saveSpy}
      />
    );

    expect(screen.getByText(/Managed externally \(Vault \/ Environment\)/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Set Custom Master Key/i })).not.toBeInTheDocument();

    rerender(
      <GeneralTab
        settings={{
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          masterKeySource: 'Vault',
        }}
        saveEmbeddingSettings={saveSpy}
      />
    );

    expect(screen.getByText(/Managed externally \(Vault\)/i)).toBeInTheDocument();
  });

  /**
   * @requirement SEC-MASTERKEY-CONFIGURED-STATUS-BADGE
   * @category SEC
   * @type PositiveFeature
   * @description Displays configured badge and rotate button when custom master key is configured.
   */
  it('renders configured badge and rotate key button when master key is Configured', async () => {
    const saveSpy = vi.fn().mockResolvedValue(true);
    const setKeySpy = vi.fn().mockResolvedValue({ success: true });

    render(
      <GeneralTab
        settings={{
          embeddingProvider: 'local',
          embeddingModelDir: 'data/models',
          embeddingApiUrl: 'http://litellm:4000/v1/embeddings',
          embeddingApiModel: 'all-MiniLM-L6-v2',
          embeddingApiKey: '',
          masterKeySource: 'Configured',
        }}
        saveEmbeddingSettings={saveSpy}
        setMasterKey={setKeySpy}
      />
    );

    expect(screen.getByText('Configured')).toBeInTheDocument();
    const rotateBtn = screen.getByRole('button', { name: /Rotate Key/i });
    expect(rotateBtn).toBeInTheDocument();

    fireEvent.click(rotateBtn);
    expect(screen.getByText('Set Master Encryption Key')).toBeInTheDocument();
  });
});

