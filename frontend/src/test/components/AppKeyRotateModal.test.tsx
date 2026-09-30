import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import AppKeyRotateModal from '../../components/clients/AppKeyRotateModal';
import { useAppKeyStore } from '../../stores/useAppKeyStore';

vi.mock('../../stores/useAppKeyStore', () => ({
  useAppKeyStore: vi.fn(),
}));

describe('AppKeyRotateModal', () => {
  const mockRotateAppKey = vi.fn();
  const mockOnClose = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    (useAppKeyStore as any).mockReturnValue({
      rotateAppKey: mockRotateAppKey,
      isRotating: false,
    });
  });

  /**
   * @requirement UI-135
   * @category UI
   * @type PositiveFeature
   * @description Renders confirmation warning on open, confirms rotation, and displays new key with Config Generator.
   */
  it('renders confirmation warning, rotates key, and shows config generator', async () => {
    mockRotateAppKey.mockResolvedValueOnce({
      id: 'key-1',
      name: 'Personal CLI',
      keyPrefix: 'mcp-usr-new',
      plaintextKey: 'mcp-usr-new-secret456',
    });

    render(
      <AppKeyRotateModal
        isOpen={true}
        keyId="key-1"
        keyName="Personal CLI"
        onClose={mockOnClose}
      />
    );

    // Stage 1: Warning must be visible
    expect(screen.getByText(/Rotating this App Key will immediately invalidate/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /confirm & rotate key/i })).toBeInTheDocument();

    // Confirm rotation
    fireEvent.click(screen.getByRole('button', { name: /confirm & rotate key/i }));

    await waitFor(() => {
      expect(mockRotateAppKey).toHaveBeenCalledWith('key-1');
    });

    // Stage 2: Key & Config Generator visible
    expect(await screen.findByText('mcp-usr-new-secret456')).toBeInTheDocument();
    expect(screen.getByText(/Claude Desktop/i)).toBeInTheDocument();
    expect(screen.getByText(/Cursor/i)).toBeInTheDocument();

    // Close button dismisses modal
    const closeBtn = screen.getByRole('button', { name: /close/i });
    fireEvent.click(closeBtn);
    expect(mockOnClose).toHaveBeenCalled();
  });

  /**
   * @requirement UI-136
   * @category UI
   * @type FailClosedGuardrail
   * @description Disables backdrop click dismissal on the key rotation modal to protect plaintext credentials.
   */
  it('prevents backdrop click dismissal', () => {
    render(
      <AppKeyRotateModal
        isOpen={true}
        keyId="key-1"
        keyName="Personal CLI"
        onClose={mockOnClose}
      />
    );

    const backdrop = screen.getByTestId('modal-backdrop');
    fireEvent.click(backdrop);

    expect(mockOnClose).not.toHaveBeenCalled();
  });
});
