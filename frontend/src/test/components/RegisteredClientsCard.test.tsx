import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RegisteredClientsCard } from '../../components/clients/RegisteredClientsCard';
import { useClientStore, RegisteredClient } from '../../stores/useClientStore';

describe('RegisteredClientsCard component', () => {
  beforeEach(() => {
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  const sampleClients: RegisteredClient[] = [
    {
      id: 'c-1',
      clientId: 'app-client-1',
      displayName: 'Postman Integration',
      clientType: 'confidential',
      isDynamic: false,
      grantTypes: ['authorization_code', 'refresh_token', 'client_credentials'],
      redirectUris: ['https://oauth.pstmn.io/v1/callback'],
      scopes: ['mcp_client', 'category:smarthome'],
      createdAt: '2026-08-30T10:00:00Z',
      expiresAt: '2026-09-30T10:00:00Z'
    },
    {
      id: 'c-2',
      clientId: 'dcr-client-2',
      displayName: 'Dynamic SPA Client',
      clientType: 'public',
      isDynamic: true,
      grantTypes: ['authorization_code'],
      redirectUris: ['http://localhost:3000/callback'],
      scopes: ['openid', 'offline_access'],
      createdAt: '2026-08-30T11:00:00Z',
      expiresAt: null
    }
  ];

  /**
   * @requirement UI-31
   * @category UI
   * @type PositiveFeature
   * @description Fetches clients on mount and renders table headers and action buttons.
   */
  it('renders header, register button, and calls fetchClients on mount', () => {
    const fetchSpy = vi.fn();
    const openModalSpy = vi.fn();
    const cleanupSpy = vi.fn();
    useClientStore.setState({
      clients: [],
      fetchClients: fetchSpy,
      cleanupClients: cleanupSpy,
      openAddClientModal: openModalSpy
    });

    render(<RegisteredClientsCard />);

    expect(screen.getByText(/Dynamic Client Registration \(RFC 7591\)/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /register client/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /clean up dcr/i })).toBeInTheDocument();
    expect(fetchSpy).toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: /register client/i }));
    expect(openModalSpy).toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: /clean up dcr/i }));
    expect(cleanupSpy).toHaveBeenCalled();
  });

  /**
   * @requirement UI-31
   * @category UI
   * @type PositiveFeature
   * @description Displays empty state when no clients are registered.
   */
  it('renders empty state when no registered clients exist', () => {
    useClientStore.setState({
      clients: [],
      fetchClients: vi.fn()
    });

    render(<RegisteredClientsCard />);

    expect(screen.getByText(/No registered clients found/i)).toBeInTheDocument();
  });

  /**
   * @requirement UI-31
   * @category UI
   * @type PositiveFeature
   * @description Renders rich OAuth client table columns: application name, client ID with copy, type badges, grant types, redirect URIs, scopes, and expiration.
   */
  it('renders rich client columns and handles client ID copy', () => {
    useClientStore.setState({
      clients: sampleClients,
      fetchClients: vi.fn()
    });

    render(<RegisteredClientsCard />);

    // Application names
    expect(screen.getByText('Postman Integration')).toBeInTheDocument();
    expect(screen.getByText('Dynamic SPA Client')).toBeInTheDocument();

    // Client IDs
    expect(screen.getByText('app-client-1')).toBeInTheDocument();
    expect(screen.getByText('dcr-client-2')).toBeInTheDocument();

    // Badges: type, dynamic vs manual
    expect(screen.getByText('confidential')).toBeInTheDocument();
    expect(screen.getByText('Manual')).toBeInTheDocument();
    expect(screen.getByText('public')).toBeInTheDocument();
    expect(screen.getByText('Dynamic')).toBeInTheDocument();

    // Grant types
    expect(screen.getByText('authorization_code, refresh_token, client_credentials')).toBeInTheDocument();

    // Redirect URIs
    expect(screen.getByText('https://oauth.pstmn.io/v1/callback')).toBeInTheDocument();
    expect(screen.getByText('http://localhost:3000/callback')).toBeInTheDocument();

    // Scopes
    expect(screen.getByText('mcp_client, category:smarthome')).toBeInTheDocument();
    expect(screen.getByText('openid, offline_access')).toBeInTheDocument();

    // Copy button
    const copyButtons = screen.getAllByRole('button', { name: /copy client id/i });
    expect(copyButtons.length).toBe(2);
    fireEvent.click(copyButtons[0]);
    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('app-client-1');
  });

  /**
   * @requirement UI-31
   * @category UI
   * @type PositiveFeature
   * @description Triggers deleteClient when Delete button is clicked.
   */
  it('triggers deleteClient when Delete button is clicked', () => {
    const deleteSpy = vi.fn();
    useClientStore.setState({
      clients: sampleClients,
      fetchClients: vi.fn(),
      deleteClient: deleteSpy
    });

    render(<RegisteredClientsCard />);

    const deleteButtons = screen.getAllByRole('button', { name: /delete/i });
    fireEvent.click(deleteButtons[0]);

    expect(deleteSpy).toHaveBeenCalledWith('c-1', 'Postman Integration');
  });

  /**
   * @requirement UI-137
   * @category UI
   * @type PositiveFeature
   * @description Renders Last Used column and triggers Rotate Secret action for registered clients.
   */
  it('renders Last Used column and triggers Rotate Secret action', async () => {
    const rotateSpy = vi.fn().mockResolvedValue({ clientId: 'app-client-1', clientSecret: 'rotated_sec_123' });
    useClientStore.setState({
      clients: [
        {
          ...sampleClients[0],
          lastUsedAt: new Date(Date.now() - 3600000).toISOString(),
        },
        {
          ...sampleClients[1],
          lastUsedAt: null,
        }
      ],
      fetchClients: vi.fn(),
      rotateClientSecret: rotateSpy,
    });

    render(<RegisteredClientsCard />);

    // Last Used table header
    expect(screen.getByRole('columnheader', { name: /Last Used/i })).toBeInTheDocument();

    // Relative date and Never badge
    expect(screen.getByText('1h ago')).toBeInTheDocument();
    expect(screen.getAllByText('Never').length).toBeGreaterThanOrEqual(1);

    // Rotate Secret button
    const rotateBtns = screen.getAllByRole('button', { name: /rotate secret/i });
    expect(rotateBtns.length).toBe(2);

    fireEvent.click(rotateBtns[0]);

    expect(rotateSpy).toHaveBeenCalledWith('c-1');
  });
});
