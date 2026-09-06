/** @requirement UI-106 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ServerCard } from '../../components/servers/ServerCard';
import { useServerStore, McpServer } from '../../stores/useServerStore';

describe('ServerCard Component', () => {
  const baseServer: McpServer = {
    id: 'docker',
    displayName: 'Docker Server',
    url: 'http://docker-mcp:8000',
    type: 'sse',
    enabled: true,
    connectionStatus: 'Connected',
    categories: ['infrastructure', 'dev'],
    hasApiKey: true,
    hidden: false,
    connectionAttempts: 0,
    connectionError: '',
    allowPassThroughAuth: false,
  };

  /**
   * @requirement MCP-01
   * @category MCP
   * @type Positive
   * @description renders connected server details with badges and triggers actions
   */
  it('renders connected server details with badges and triggers actions', () => {
    const toggleSpy = vi.spyOn(useServerStore.getState(), 'toggleServerEnabled').mockImplementation(vi.fn());
    const editSpy = vi.spyOn(useServerStore.getState(), 'openEditModal').mockImplementation(vi.fn());
    const inspectSpy = vi.spyOn(useServerStore.getState(), 'openInspectModal').mockImplementation(vi.fn());
    const deleteSpy = vi.spyOn(useServerStore.getState(), 'deleteServer').mockImplementation(vi.fn());

    render(<ServerCard server={baseServer} />);

    expect(screen.getByText('Docker Server')).toBeInTheDocument();
    expect(screen.getByText('SSE')).toBeInTheDocument();
    expect(screen.getByText('infrastructure')).toBeInTheDocument();
    expect(screen.getByText('Connected')).toBeInTheDocument();
    expect(screen.getByText('Secured')).toBeInTheDocument();

    // Trigger Inspect
    const inspectBtn = screen.getByTitle(/Inspect Capabilities/i);
    fireEvent.click(inspectBtn);
    expect(inspectSpy).toHaveBeenCalledWith(baseServer);

    // Trigger Edit
    const editBtn = screen.getByTitle(/Edit Server Config/i);
    fireEvent.click(editBtn);
    expect(editSpy).toHaveBeenCalledWith(baseServer);

    // Trigger Delete
    const deleteBtn = screen.getByTitle(/Delete Server/i);
    fireEvent.click(deleteBtn);
    expect(deleteSpy).toHaveBeenCalledWith('docker', 'Docker Server');

    // Trigger Toggle Enabled
    const switchInput = screen.getByRole('checkbox');
    expect(switchInput).toBeChecked();
    fireEvent.click(switchInput);
    expect(toggleSpy).toHaveBeenCalledWith('docker', false);
  });

  /**
   * @requirement MCP-01
   * @category MCP
   * @type Positive
   * @description renders connecting/retrying state
   */
  it('renders connecting/retrying state', () => {
    const connectingServer: McpServer = {
      ...baseServer,
      connectionStatus: 'Connecting',
      connectionAttempts: 2,
    };

    render(<ServerCard server={connectingServer} />);
    expect(screen.getByText(/Connecting \(2\/5\)/i)).toBeInTheDocument();
  });

  /**
   * @requirement MCP-01
   * @category MCP
   * @type Positive
   * @description renders failed state with retry button
   */
  it('renders failed state with retry button', () => {
    const reconnectSpy = vi.spyOn(useServerStore.getState(), 'reconnectServer').mockImplementation(vi.fn());
    const failedServer: McpServer = {
      ...baseServer,
      connectionStatus: 'Failed',
      connectionError: 'Port 8000 timeout',
      connectionAttempts: 3,
    };

    render(<ServerCard server={failedServer} />);
    expect(screen.getByText('Failed')).toBeInTheDocument();

    const retryBtn = screen.getByTitle(/Retry Connection/i);
    fireEvent.click(retryBtn);
    expect(reconnectSpy).toHaveBeenCalledWith('docker');
  });

  /**
   * @requirement MCP-01
   * @category MCP
   * @type Positive
   * @description renders disconnected state with connect button and hidden badge
   */
  it('renders disconnected state with connect button and hidden badge', () => {
    const reconnectSpy = vi.spyOn(useServerStore.getState(), 'reconnectServer').mockImplementation(vi.fn());
    const disconnectedServer: McpServer = {
      ...baseServer,
      connectionStatus: 'Disconnected',
      hidden: true,
    };

    render(<ServerCard server={disconnectedServer} />);
    expect(screen.getByText('Disconnected')).toBeInTheDocument();
    expect(screen.getByText('Hidden')).toBeInTheDocument();

    const connectBtn = screen.getByTitle('Connect Server');
    fireEvent.click(connectBtn);
    expect(reconnectSpy).toHaveBeenCalledWith('docker');
  });

  /**
   * @requirement MCP-01
   * @category MCP
   * @type Positive
   * @description renders disabled state
   */
  it('renders disabled state', () => {
    const disabledServer: McpServer = {
      ...baseServer,
      enabled: false,
    };

    render(<ServerCard server={disabledServer} />);
    expect(screen.getByText('Disabled')).toBeInTheDocument();
  });

  /**
   * @requirement UI-SERVERS-ALIAS-MANAGEMENT
   * @category MCP
   * @type Positive
   * @description renders server alias badge alongside server id when configured
   */
  it('renders server alias badge alongside server id when configured (UI-SERVERS-ALIAS-MANAGEMENT)', () => {
    const serverWithAlias: McpServer = {
      ...baseServer,
      id: 'docker-id',
      alias: 'docker_namespace',
    };

    render(<ServerCard server={serverWithAlias} />);
    expect(screen.getByText('docker-id')).toBeInTheDocument();
    expect(screen.getByText('docker_namespace')).toBeInTheDocument();
  });
});
