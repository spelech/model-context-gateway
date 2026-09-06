/** @requirement UI-SERVERS-ALIAS-MANAGEMENT */

import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ServerModal } from './ServerModal';
import { ServerTable } from './ServerTable';
import { McpServer } from '../../shared/types';

describe('ServerModal Alias Field (UI-SERVERS-ALIAS-MANAGEMENT)', () => {
  it('renders Alias input and validates characters', async () => {
    render(<ServerModal isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    const aliasInput = screen.getByLabelText(/alias/i);
    expect(aliasInput).toBeInTheDocument();

    fireEvent.change(aliasInput, { target: { value: 'invalid alias!@#' } });
    expect(screen.getByText(/letters, numbers, underscores, and hyphens/i)).toBeInTheDocument();
  });

  it('displays helper text explaining routing namespace (UI-SERVERS-ALIAS-MANAGEMENT)', () => {
    render(<ServerModal isOpen={true} onClose={vi.fn()} onSave={vi.fn()} />);

    expect(screen.getByText(/routing namespace/i)).toBeInTheDocument();
    expect(screen.getByText(/homebox_db/i)).toBeInTheDocument();
  });

  it('populates alias when editing server with existing alias (UI-SERVERS-ALIAS-MANAGEMENT)', () => {
    const existingServer: McpServer = {
      id: 'srv-1',
      alias: 'homebox_db',
      displayName: 'Homebox Server',
      url: 'http://localhost:8000',
      type: 'sse',
      enabled: true,
      hidden: false,
      categories: ['database'],
      hasApiKey: false,
      connectionStatus: 'Connected',
      connectionAttempts: 0,
      connectionError: '',
      allowPassThroughAuth: false,
    };

    render(<ServerModal isOpen={true} server={existingServer} onClose={vi.fn()} onSave={vi.fn()} />);

    const aliasInput = screen.getByLabelText(/alias/i);
    expect(aliasInput).toHaveValue('homebox_db');
  });

  it('submits valid alias successfully (UI-SERVERS-ALIAS-MANAGEMENT)', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(<ServerModal isOpen={true} onClose={vi.fn()} onSave={onSave} />);

    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: 'Test Server' } });
    fireEvent.change(screen.getByLabelText(/connection url/i), { target: { value: 'http://test:8000/sse' } });
    fireEvent.change(screen.getByLabelText(/alias/i), { target: { value: 'valid_alias-1' } });

    const submitBtn = screen.getByRole('button', { name: /save server/i });
    await act(async () => {
      fireEvent.click(submitBtn);
    });

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({
        displayName: 'Test Server',
        alias: 'valid_alias-1',
      })
    );
  });

  it('blocks submit when alias format is invalid (UI-SERVERS-ALIAS-MANAGEMENT)', async () => {
    const onSave = vi.fn();
    render(<ServerModal isOpen={true} onClose={vi.fn()} onSave={onSave} />);

    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: 'Test Server' } });
    fireEvent.change(screen.getByLabelText(/connection url/i), { target: { value: 'http://test:8000/sse' } });
    fireEvent.change(screen.getByLabelText(/alias/i), { target: { value: 'invalid alias spaces' } });

    const submitBtn = screen.getByRole('button', { name: /save server/i });
    await act(async () => {
      fireEvent.click(submitBtn);
    });

    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByText(/letters, numbers, underscores, and hyphens/i)).toBeInTheDocument();
  });
});

describe('ServerTable Badge Rendering (UI-SERVERS-ALIAS-MANAGEMENT)', () => {
  it('renders alias badge alongside server id in table view', () => {
    const testServers: McpServer[] = [
      {
        id: 'homebox',
        alias: 'homebox_alias',
        displayName: 'Homebox Server',
        url: 'http://homebox:7745',
        type: 'sse',
        enabled: true,
        hidden: false,
        categories: ['inventory'],
        hasApiKey: false,
        connectionStatus: 'Connected',
        connectionAttempts: 0,
        connectionError: '',
        allowPassThroughAuth: false,
      },
      {
        id: 'vault',
        displayName: 'Vault Server',
        url: 'http://vault:8200',
        type: 'sse',
        enabled: true,
        hidden: false,
        categories: ['secrets'],
        hasApiKey: false,
        connectionStatus: 'Connected',
        connectionAttempts: 0,
        connectionError: '',
        allowPassThroughAuth: false,
      },
    ];

    render(<ServerTable servers={testServers} />);

    expect(screen.getByText('homebox')).toBeInTheDocument();
    expect(screen.getAllByText('homebox_alias').length).toBeGreaterThan(0);
    expect(screen.getByText('vault')).toBeInTheDocument();
  });
});
