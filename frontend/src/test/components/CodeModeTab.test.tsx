import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CodeModeTab } from '../../components/settings/CodeModeTab';

describe('CodeModeTab', () => {
  /**
   * @requirement UI-CODE-01
   * @category UI
   * @type PositiveFeature
   * @description CodeModeTab renders master toggle, quota sliders, and group selector.
   */
  it('renders code mode settings and triggers update on toggle', () => {
    const mockSave = vi.fn();
    render(
      <CodeModeTab
        settings={{
          enableCodeMode: true,
          codeModeExecutionTimeoutSeconds: 30,
          codeModeMaxToolCalls: 50,
          codeModeMaxMemoryMb: 64,
          codeModeMaxStatements: 100000,
          codeModeAllowedGroups: 'full_admin',
        }}
        availableGroups={['full_admin', 'devs', 'users']}
        onSave={mockSave}
      />
    );

    expect(screen.getByText(/Code Mode/i)).toBeInTheDocument();
    const toggle = screen.getByTestId('toggle-enable-code-mode');
    expect(toggle).toBeChecked();
    expect(screen.getByText('full_admin')).toBeInTheDocument();

    // Check sliders / inputs
    const timeoutInput = screen.getByTestId('input-timeout');
    expect(timeoutInput).toHaveValue('30');
    fireEvent.change(timeoutInput, { target: { value: '45' } });
    expect(timeoutInput).toHaveValue('45');

    // Toggle switch off
    fireEvent.click(toggle);
    expect(toggle).not.toBeChecked();
  });

  /**
   * @requirement UI-CODE-02
   * @category UI
   * @type PositiveFeature
   * @description CodeModeTab allows adding and removing allowed groups and saves changes.
   */
  it('allows adding and removing allowed groups and triggers onSave', async () => {
    const mockSave = vi.fn().mockResolvedValue(true);
    render(
      <CodeModeTab
        settings={{
          enableCodeMode: true,
          codeModeExecutionTimeoutSeconds: 30,
          codeModeMaxToolCalls: 50,
          codeModeMaxMemoryMb: 64,
          codeModeMaxStatements: 100000,
          codeModeAllowedGroups: 'full_admin',
        }}
        availableGroups={['full_admin', 'devs', 'users']}
        onSave={mockSave}
      />
    );

    // Remove 'full_admin' group
    const removeBtn = screen.getByTestId('remove-group-full_admin');
    await act(async () => {
      fireEvent.click(removeBtn);
    });
    expect(screen.queryByText('full_admin')).not.toBeInTheDocument();

    // When empty, shows "Allowed for all authenticated clients"
    expect(screen.getByText(/Allowed for all authenticated clients/i)).toBeInTheDocument();

    // Add a group from input
    const input = screen.getByTestId('input-add-group');
    await act(async () => {
      fireEvent.change(input, { target: { value: 'devs' } });
      fireEvent.keyDown(input, { key: 'Enter', code: 'Enter' });
    });
    expect(screen.getByText('devs')).toBeInTheDocument();

    // Add another group from suggested select dropdown
    const select = screen.getByTestId('select-suggested-group');
    await act(async () => {
      fireEvent.change(select, { target: { value: 'users' } });
    });
    expect(screen.getByText('users')).toBeInTheDocument();

    // Save changes
    const saveBtn = screen.getByTestId('btn-save-code-mode');
    await act(async () => {
      fireEvent.click(saveBtn);
    });

    expect(mockSave).toHaveBeenCalledTimes(1);
    expect(mockSave).toHaveBeenCalledWith(
      expect.objectContaining({
        enableCodeMode: true,
        codeModeAllowedGroups: 'devs,users',
      })
    );
  });
});
