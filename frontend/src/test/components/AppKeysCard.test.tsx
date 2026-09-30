import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { AppKeysCard } from '../../components/clients/AppKeysCard';
import { useAppKeyStore } from '../../stores/useAppKeyStore';
import { useUserStore } from '../../stores/useUserStore';

vi.mock('../../stores/useAppKeyStore', () => ({
  useAppKeyStore: vi.fn(),
}));

describe('AppKeysCard Component', () => {
  const fetchAppKeysMock = vi.fn();
  const fetchLimitsMock = vi.fn();
  const fetchUserQuotasMock = vi.fn();
  const setUserQuotaMock = vi.fn();
  const deleteUserQuotaMock = vi.fn();
  const revokeAppKeyMock = vi.fn();
  const openModalMock = vi.fn();
  const setKeyTypeTabMock = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    useUserStore.setState({
      user: {
        authenticated: true,
        username: 'steve',
        name: 'Steve Pelech',
        groups: ['full_admin']
      }
    });
  });

  /**
   * @requirement AUTH-PERSONAL-APPKEY-LIST
   * @category AUTH
   * @type PositiveFeature
   * @description Renders empty state and triggers fetch on mount for non-admin users without owner column.
   */
  it('renders role-adapted My App Keys view for non-admin user', () => {
    useUserStore.setState({
      user: {
        authenticated: true,
        username: 'alice',
        name: 'Alice Cooper',
        groups: ['developer']
      }
    });

    (useAppKeyStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      appKeys: [],
      limits: { userActiveKeys: 0, userMax: 5, totalActiveKeys: 0, globalMax: 50, isLimitReached: false },
      keyTypeTab: 'personal',
      userQuotas: [],
      fetchAppKeys: fetchAppKeysMock,
      fetchLimits: fetchLimitsMock,
      fetchUserQuotas: fetchUserQuotasMock,
      setUserQuota: setUserQuotaMock,
      deleteUserQuota: deleteUserQuotaMock,
      revokeAppKey: revokeAppKeyMock,
      openModal: openModalMock,
      setKeyTypeTab: setKeyTypeTabMock,
    });

    render(<AppKeysCard />);

    expect(screen.getByRole('heading', { name: /My App Keys/i })).toBeInTheDocument();
    expect(screen.getByText(/Personal Quota:/i)).toBeInTheDocument();
    expect(screen.getByText(/0 \/ 5/i)).toBeInTheDocument();
    expect(screen.getByText(/No App Keys active/i)).toBeInTheDocument();
    // Non-admin view must not show Owner column in table header
    expect(screen.queryByRole('columnheader', { name: /^Owner$/i })).toBeNull();
    // Non-admin view must not show admin sub-tabs
    expect(screen.queryByRole('button', { name: /App-Level Keys/i })).toBeNull();
    expect(fetchAppKeysMock).toHaveBeenCalledWith('personal');
    expect(fetchLimitsMock).toHaveBeenCalled();
  });

  /**
   * @requirement AUTH-PERSONAL-APPKEY-LIST
   * @category AUTH
   * @type PositiveFeature
   * @description Renders keys list, opens rotate modal, and revokes key.
   */
  it('renders keys list, opens rotate modal, and revokes key', async () => {
    (useAppKeyStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      appKeys: [
        {
          id: 'k1',
          name: 'Active Key',
          keyPrefix: 'mcp_live_123',
          username: 'steve',
          scopes: ['category:smarthome'],
          expiresAt: new Date(Date.now() + 86400000).toISOString(),
          createdAt: new Date().toISOString(),
        },
        {
          id: 'k2',
          name: 'Expired Key',
          keyPrefix: 'mcp_live_999',
          username: 'steve',
          scopes: [],
          expiresAt: '2020-01-01T00:00:00Z',
          createdAt: '2019-12-01T00:00:00Z',
        },
        {
          id: 'k3',
          name: 'Never Expiring Key',
          keyPrefix: 'mcp_live_000',
          username: 'admin',
          scopes: null,
          expiresAt: null,
          createdAt: new Date().toISOString(),
        }
      ],
      limits: { userActiveKeys: 3, userMax: 5, totalActiveKeys: 10, globalMax: 50, isLimitReached: false },
      keyTypeTab: 'personal',
      userQuotas: [],
      fetchAppKeys: fetchAppKeysMock,
      fetchLimits: fetchLimitsMock,
      fetchUserQuotas: fetchUserQuotasMock,
      setUserQuota: setUserQuotaMock,
      deleteUserQuota: deleteUserQuotaMock,
      revokeAppKey: revokeAppKeyMock,
      openModal: openModalMock,
      setKeyTypeTab: setKeyTypeTabMock,
      rotateAppKey: vi.fn(),
      isRotating: false,
    });

    render(<AppKeysCard />);

    expect(screen.getByText('Active Key')).toBeInTheDocument();
    expect(screen.getByText('Expired')).toBeInTheDocument();
    expect(screen.getAllByText('Never').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('category:smarthome')).toBeInTheDocument();
    // Admin view shows Owner column
    expect(screen.getByRole('columnheader', { name: /^Owner$/i })).toBeInTheDocument();

    // Test Rotate button
    const rotateBtns = screen.getAllByTitle(/rotate key/i);
    await act(async () => {
      fireEvent.click(rotateBtns[0]);
    });
    expect(screen.getByRole('heading', { name: /Rotate App Key/i })).toBeInTheDocument();

    // Test Revoke button
    const revokeBtns = screen.getAllByRole('button', { name: /revoke/i });
    await act(async () => {
      fireEvent.click(revokeBtns[0]);
    });
    expect(revokeAppKeyMock).toHaveBeenCalledWith('k1', 'Active Key');

    // Test Create App Key button
    const createBtn = screen.getByRole('button', { name: /create app key/i });
    fireEvent.click(createBtn);
    expect(openModalMock).toHaveBeenCalled();
  });

  /**
   * @requirement AUTH-SYSTEM-APPKEY-SEPARATION
   * @category AUTH
   * @type PositiveFeature
   * @description Allows admin to switch to App-Level Keys tab and filter personal keys by username.
   */
  it('handles admin tab switching and username filtering', async () => {
    (useAppKeyStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      appKeys: [],
      limits: { userActiveKeys: 1, userMax: 5, totalActiveKeys: 5, globalMax: 50, isLimitReached: false },
      keyTypeTab: 'personal',
      userQuotas: [],
      fetchAppKeys: fetchAppKeysMock,
      fetchLimits: fetchLimitsMock,
      fetchUserQuotas: fetchUserQuotasMock,
      setUserQuota: setUserQuotaMock,
      deleteUserQuota: deleteUserQuotaMock,
      revokeAppKey: revokeAppKeyMock,
      openModal: openModalMock,
      setKeyTypeTab: setKeyTypeTabMock,
    });

    render(<AppKeysCard />);

    // Switch to App-Level Keys
    const systemTabBtn = screen.getByRole('button', { name: /App-Level Keys/i });
    await act(async () => {
      fireEvent.click(systemTabBtn);
    });
    expect(setKeyTypeTabMock).toHaveBeenCalledWith('system');
    expect(fetchAppKeysMock).toHaveBeenCalledWith('system', undefined);

    // Switch back to Personal Keys
    const personalTabBtn = screen.getByRole('button', { name: /User Personal Keys/i });
    await act(async () => {
      fireEvent.click(personalTabBtn);
    });
    expect(setKeyTypeTabMock).toHaveBeenCalledWith('personal');

    // Filter by username
    const filterInput = screen.getByPlaceholderText(/Filter by username\.\.\./i);
    fireEvent.change(filterInput, { target: { value: 'alice' } });
    const filterBtn = screen.getByRole('button', { name: /Filter/i });
    await act(async () => {
      fireEvent.click(filterBtn);
    });
    expect(fetchAppKeysMock).toHaveBeenCalledWith('personal', 'alice');

    // Clear filter
    const clearBtn = screen.getByRole('button', { name: /Clear/i });
    await act(async () => {
      fireEvent.click(clearBtn);
    });
    expect(fetchAppKeysMock).toHaveBeenCalledWith('personal');
  });

  /**
   * @requirement AUTH-PERSONAL-APPKEY-QUOTA-OVERRIDE
   * @category AUTH
   * @type PositiveFeature
   * @description Allows admin to view custom user quotas, set a quota override, and reset a quota.
   */
  it('manages custom user quotas in admin quotas tab', async () => {
    setUserQuotaMock.mockResolvedValue(undefined);
    deleteUserQuotaMock.mockResolvedValue(undefined);

    (useAppKeyStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      appKeys: [],
      limits: { userActiveKeys: 1, userMax: 5, totalActiveKeys: 5, globalMax: 50, isLimitReached: false },
      keyTypeTab: 'personal',
      userQuotas: [
        {
          username: 'special_user',
          maxKeys: 25,
          createdAt: '2026-08-01T00:00:00Z',
          updatedAt: '2026-08-02T00:00:00Z'
        }
      ],
      fetchAppKeys: fetchAppKeysMock,
      fetchLimits: fetchLimitsMock,
      fetchUserQuotas: fetchUserQuotasMock,
      setUserQuota: setUserQuotaMock,
      deleteUserQuota: deleteUserQuotaMock,
      revokeAppKey: revokeAppKeyMock,
      openModal: openModalMock,
      setKeyTypeTab: setKeyTypeTabMock,
    });

    render(<AppKeysCard />);

    // Click Custom User Quotas tab
    const quotasTabBtn = screen.getByRole('button', { name: /Custom User Quotas/i });
    await act(async () => {
      fireEvent.click(quotasTabBtn);
    });
    expect(fetchUserQuotasMock).toHaveBeenCalled();
    expect(screen.getByText('special_user')).toBeInTheDocument();
    expect(screen.getByText('25 keys')).toBeInTheDocument();

    // Set new quota
    const usernameInput = screen.getByLabelText(/Username/i);
    const maxKeysInput = screen.getByLabelText(/Max Keys/i);
    fireEvent.change(usernameInput, { target: { value: 'power_dev' } });
    fireEvent.change(maxKeysInput, { target: { value: '15' } });

    const setQuotaBtn = screen.getByRole('button', { name: /Set Quota/i });
    await act(async () => {
      fireEvent.click(setQuotaBtn);
    });
    expect(setUserQuotaMock).toHaveBeenCalledWith('power_dev', 15);

    // Reset quota
    const resetBtn = screen.getByRole('button', { name: /Reset/i });
    await act(async () => {
      fireEvent.click(resetBtn);
    });
    expect(deleteUserQuotaMock).toHaveBeenCalledWith('special_user');
  });

  /**
   * @requirement UI-137
   * @category UI
   * @type PositiveFeature
   * @description Renders Last Used column and replaces static config button with Rotate Key action opening rotation modal.
   */
  it('renders Last Used column and triggers Rotate Key modal', async () => {
    (useAppKeyStore as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
      appKeys: [
        {
          id: 'k-1',
          name: 'Personal Key 1',
          keyPrefix: 'mcp_live_123',
          username: 'steve',
          scopes: ['category:smarthome'],
          expiresAt: null,
          createdAt: new Date().toISOString(),
          lastUsedAt: new Date(Date.now() - 3600000).toISOString(),
        },
        {
          id: 'k-2',
          name: 'Never Used Key',
          keyPrefix: 'mcp_live_456',
          username: 'steve',
          scopes: [],
          expiresAt: null,
          createdAt: new Date().toISOString(),
          lastUsedAt: null,
        }
      ],
      limits: { userActiveKeys: 2, userMax: 5, totalActiveKeys: 2, globalMax: 50, isLimitReached: false },
      keyTypeTab: 'personal',
      userQuotas: [],
      fetchAppKeys: fetchAppKeysMock,
      fetchLimits: fetchLimitsMock,
      fetchUserQuotas: fetchUserQuotasMock,
      setUserQuota: setUserQuotaMock,
      deleteUserQuota: deleteUserQuotaMock,
      revokeAppKey: revokeAppKeyMock,
      openModal: openModalMock,
      setKeyTypeTab: setKeyTypeTabMock,
      rotateAppKey: vi.fn(),
      isRotating: false,
    });

    render(<AppKeysCard />);

    // Last Used table header
    expect(screen.getByRole('columnheader', { name: /Last Used/i })).toBeInTheDocument();

    // Check Last Used values
    expect(screen.getByText('1h ago')).toBeInTheDocument();
    expect(screen.getAllByText('Never').length).toBeGreaterThanOrEqual(1);

    // Click Rotate button
    const rotateBtns = screen.getAllByRole('button', { name: /Rotate/i });
    expect(rotateBtns.length).toBeGreaterThan(0);
    await act(async () => {
      fireEvent.click(rotateBtns[0]);
    });

    // Rotation modal should open
    expect(screen.getByRole('heading', { name: /Rotate App Key/i })).toBeInTheDocument();
    expect(screen.getByText(/Key to rotate:/i)).toBeInTheDocument();
    expect(screen.getAllByText('Personal Key 1').length).toBe(2);
  });
});
