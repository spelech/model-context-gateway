/** @requirement UI-114 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { PolicyModal } from '../../components/security/PolicyModal';
import { useSettingsStore, AccessPolicy } from '../../stores/useSettingsStore';

describe('PolicyModal component', () => {
  const existingPolicy: AccessPolicy = {
    id: 'pol-100',
    targetId: 'tool:docker__restart_container',
    requiredGroup: 'docker_admins',
    isAllowed: true
  };

  /**
   * @requirement UI-POLICY-MODAL-VISIBILITY
   * @category AUTH
   * @type Positive
   * @description renders nothing when isPolicyModalOpen is false
   */
  it('renders nothing when isPolicyModalOpen is false', () => {
    useSettingsStore.setState({ isPolicyModalOpen: false, editingPolicy: null });
    const { container } = render(<PolicyModal />);
    expect(container.firstChild).toBeNull();
  });

  /**
   * @requirement UI-POLICY-MODAL-CREATE-DEFAULTS
   * @category AUTH
   * @type Positive
   * @description renders create policy form with default inputs
   */
  it('renders create policy form with default inputs', () => {
    useSettingsStore.setState({ isPolicyModalOpen: true, editingPolicy: null });
    render(<PolicyModal />);

    expect(screen.getByText('Create Access Policy')).toBeInTheDocument();
    expect(screen.getByLabelText('Target ID')).toHaveValue('');
    expect(screen.getByLabelText('Required Group / Internal Group')).toHaveValue('');
    expect(screen.getByLabelText('Policy Mode')).toHaveValue('true');
  });

  /**
   * @requirement UI-POLICY-MODAL-EDIT-PREFILL
   * @category AUTH
   * @type Positive
   * @description renders edit policy form pre-filled with policy data
   */
  it('renders edit policy form pre-filled with policy data', () => {
    useSettingsStore.setState({ isPolicyModalOpen: true, editingPolicy: existingPolicy });
    render(<PolicyModal />);

    expect(screen.getByText('Edit Access Policy')).toBeInTheDocument();
    expect(screen.getByLabelText('Target ID')).toHaveValue('tool:docker__restart_container');
    expect(screen.getByLabelText('Required Group / Internal Group')).toHaveValue('docker_admins');
    expect(screen.getByLabelText('Policy Mode')).toHaveValue('true');
  });

  /**
   * @requirement GUARD-01
   * @category GUARD
   * @type Negative
   * @description submits form with constructed payload for DENY policy
   */
  it('submits form with constructed payload for DENY policy', async () => {
    const saveSpy = vi.fn().mockResolvedValue(undefined);
    useSettingsStore.setState({ isPolicyModalOpen: true, editingPolicy: null, savePolicy: saveSpy });
    render(<PolicyModal />);

    fireEvent.change(screen.getByLabelText('Target ID'), { target: { value: 'server:plex' } });
    fireEvent.change(screen.getByLabelText('Required Group / Internal Group'), { target: { value: 'restricted_users' } });
    fireEvent.change(screen.getByLabelText('Policy Mode'), { target: { value: 'false' } });

    const saveBtn = screen.getByRole('button', { name: /save policy/i });
    await act(async () => {
      fireEvent.click(saveBtn);
    });

    expect(saveSpy).toHaveBeenCalledWith({
      targetId: 'server:plex',
      requiredGroup: 'restricted_users',
      isAllowed: false
    });
  });

  /**
   * @requirement UI-POLICY-MODAL-CANCEL-DISMISS
   * @category AUTH
   * @type Positive
   * @description closes modal on cancel click
   */
  it('closes modal on cancel click', () => {
    const closeSpy = vi.fn();
    useSettingsStore.setState({ isPolicyModalOpen: true, editingPolicy: null, closePolicyModal: closeSpy });
    render(<PolicyModal />);

    const cancelBtn = screen.getByRole('button', { name: /cancel/i });
    fireEvent.click(cancelBtn);

    expect(closeSpy).toHaveBeenCalled();
  });
});
