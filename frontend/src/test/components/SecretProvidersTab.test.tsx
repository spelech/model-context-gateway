import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { SecretProvidersTab } from '../../components/settings/SecretProvidersTab';
import * as settingsApi from '../../api/settingsApi';
import { useToastStore } from '../../stores/useToastStore';

vi.mock('../../api/settingsApi', () => ({
  testVaultConnectionApi: vi.fn(),
}));

describe('SecretProvidersTab Component', () => {
  const saveSecretProviderMock = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    useToastStore.setState({ toasts: [] });
  });

  /**
   * @requirement SEC-01
   * @category SEC
   * @type PositiveFeature
   * @description Renders Vault, WindowsRegistry, and Environment secret provider settings and saves configuration.
   */
  it('renders provider inputs and submits updated configuration', async () => {
    const providers = [
      { providerName: 'Vault', displayName: 'HashiCorp Vault', isEnabled: true, configJson: '{"address":"http://127.0.0.1:8200","token":"root-tok","mountPath":"secret/data/"}' },
      { providerName: 'WindowsRegistry', displayName: 'Windows Registry', isEnabled: false, configJson: '{"keyPath":"SOFTWARE\\\\McpRouter"}' },
      { providerName: 'Environment', displayName: 'System Environment', isEnabled: true, configJson: '{"prefix":"MCP_"}' },
    ];

    render(
      <SecretProvidersTab
        providers={providers}
        saveSecretProvider={saveSecretProviderMock}
      />
    );

    expect(screen.getByText('Secret Providers')).toBeInTheDocument();
    expect(screen.getByText('HashiCorp Vault')).toBeInTheDocument();
    expect(screen.getByText('DPAPI Encrypted')).toBeInTheDocument();

    // Switch to AppRole auth
    const approleRadio = screen.getByLabelText(/AppRole Auth/i);
    fireEvent.click(approleRadio);

    // Fill Role ID and Secret ID
    const roleIdInput = screen.getByPlaceholderText('Role ID');
    const secretIdInput = screen.getByPlaceholderText('Secret ID');
    fireEvent.change(roleIdInput, { target: { value: 'test-role-id' } });
    fireEvent.change(secretIdInput, { target: { value: 'test-secret-id' } });

    // Submit form
    const saveBtn = screen.getByRole('button', { name: /save secret config/i });
    await act(async () => {
      fireEvent.click(saveBtn);
    });

    expect(saveSecretProviderMock).toHaveBeenCalledTimes(4);
    expect(useToastStore.getState().toasts.some((t) => t.message.includes('Secret Provider configurations saved successfully') && t.type === 'success')).toBe(true);
  });

  /**
   * @requirement UI-TOAST-TRANSITION
   * @category UI
   * @type FailClosedGuardrail
   * @description Displays error toast notification when saving secret providers fails.
   */
  it('displays error toast when saving secret providers fails', async () => {
    const failingSaveMock = vi.fn().mockRejectedValue(new Error('Save failed'));
    const providers = [
      { providerName: 'Vault', displayName: 'HashiCorp Vault', isEnabled: true, configJson: '{"address":"http://127.0.0.1:8200"}' },
    ];

    render(
      <SecretProvidersTab
        providers={providers}
        saveSecretProvider={failingSaveMock}
      />
    );

    const saveBtn = screen.getByRole('button', { name: /save secret config/i });
    await act(async () => {
      fireEvent.click(saveBtn);
    });

    expect(useToastStore.getState().toasts.some((t) => t.message.includes('Save failed') && t.type === 'error')).toBe(true);
  });

  /**
   * @requirement SEC-01
   * @category SEC
   * @type PositiveFeature
   * @description Handles Vault connection testing success and failure feedback.
   */
  it('handles Test Vault connection button with success and failure responses', async () => {
    const testVaultSpy = vi.spyOn(settingsApi, 'testVaultConnectionApi');

    // Test Success
    testVaultSpy.mockResolvedValueOnce({ success: true, message: 'Vault connected OK!' });

    const providers = [
      { providerName: 'Vault', displayName: 'HashiCorp Vault', isEnabled: true, configJson: '{"address":"http://127.0.0.1:8200"}' },
    ];

    render(
      <SecretProvidersTab
        providers={providers}
        saveSecretProvider={saveSecretProviderMock}
      />
    );

    const testBtn = screen.getByRole('button', { name: /test vault/i });
    await act(async () => {
      fireEvent.click(testBtn);
    });

    expect(testVaultSpy).toHaveBeenCalled();
    expect(screen.getByText('Vault connected OK!')).toBeInTheDocument();

    // Test Failure
    testVaultSpy.mockResolvedValueOnce({ success: false, error: 'Vault sealed or unreachable' });
    await act(async () => {
      fireEvent.click(testBtn);
    });
    expect(screen.getByText('Vault sealed or unreachable')).toBeInTheDocument();

    // Test Network Exception
    testVaultSpy.mockRejectedValueOnce(new Error('Network timeout'));
    await act(async () => {
      fireEvent.click(testBtn);
    });
    expect(screen.getByText('Network timeout')).toBeInTheDocument();
  });

  /**
   * @requirement SEC-30
   * @category SEC
   * @type PositiveFeature
   * @description Renders User Secret Storage options and toggles Vault with warning when disabled.
   */
  it('renders User Secret Storage options and toggles Vault with warning when disabled', async () => {
    const providers = [
      { providerName: 'Vault', displayName: 'HashiCorp Vault', isEnabled: false, configJson: '{"address":"http://127.0.0.1:8200"}' },
    ];

    render(
      <SecretProvidersTab
        providers={providers}
        saveSecretProvider={saveSecretProviderMock}
      />
    );

    expect(screen.getByText('User Secret Storage (BYOK)')).toBeInTheDocument();
    expect(screen.getByLabelText(/Database \(Encrypted Storage\)/i)).toBeChecked();

    // Select Vault
    const vaultRadio = screen.getByLabelText(/HashiCorp Vault \(KV v2\)/i);
    fireEvent.click(vaultRadio);
    expect(vaultRadio).toBeChecked();

    // Warning is visible because Vault is disabled
    expect(screen.getByText(/HashiCorp Vault is selected for User Secret Storage, but Vault Secret Provider is disabled/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Vault Path Template:/i)).toBeInTheDocument();
  });
});
