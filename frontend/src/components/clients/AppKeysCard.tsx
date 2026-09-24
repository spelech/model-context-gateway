import React, { useEffect, useState } from 'react';
import { useAppKeyStore } from '../../stores/useAppKeyStore';
import { useUserStore } from '../../stores/useUserStore';
import { showToast } from '../../stores/useToastStore';

export const AppKeysCard: React.FC = () => {
  const { user } = useUserStore();
  const isAdmin = !!(user?.groups && user.groups.includes('full_admin'));

  const {
    appKeys,
    limits,
    keyTypeTab,
    userQuotas,
    fetchAppKeys,
    fetchLimits,
    fetchUserQuotas,
    setUserQuota,
    deleteUserQuota,
    revokeAppKey,
    openModal,
    setKeyTypeTab
  } = useAppKeyStore();

  const [activeTab, setActiveTab] = useState<'personal' | 'system' | 'quotas'>(keyTypeTab || 'personal');
  const [usernameFilter, setUsernameFilter] = useState('');
  const [quotaUsername, setQuotaUsername] = useState('');
  const [quotaMaxKeys, setQuotaMaxKeys] = useState<number>(5);
  const [isSubmittingQuota, setIsSubmittingQuota] = useState(false);

  useEffect(() => {
    fetchLimits();
    if (isAdmin) {
      if (activeTab === 'quotas') {
        fetchUserQuotas();
      } else {
        fetchAppKeys(activeTab);
      }
    } else {
      fetchAppKeys('personal');
    }
  }, [fetchAppKeys, fetchLimits, fetchUserQuotas, isAdmin, activeTab]);

  const handleTabSwitch = (tab: 'personal' | 'system' | 'quotas') => {
    setActiveTab(tab);
    if (tab === 'personal' || tab === 'system') {
      setKeyTypeTab(tab);
      fetchAppKeys(tab, tab === 'personal' ? (usernameFilter.trim() || undefined) : undefined);
    } else if (tab === 'quotas') {
      fetchUserQuotas();
    }
  };

  const handleFilter = (e: React.FormEvent) => {
    e.preventDefault();
    if (activeTab === 'personal') {
      fetchAppKeys('personal', usernameFilter.trim() || undefined);
    }
  };

  const handleClearFilter = () => {
    setUsernameFilter('');
    fetchAppKeys('personal');
  };

  const handleSaveQuota = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!quotaUsername.trim()) return;
    setIsSubmittingQuota(true);
    try {
      await setUserQuota(quotaUsername.trim(), quotaMaxKeys);
      setQuotaUsername('');
      setQuotaMaxKeys(5);
    } catch (err) {
      console.error(err);
    } finally {
      setIsSubmittingQuota(false);
    }
  };

  const copyConfigSnippet = (keyPrefix: string) => {
    const sampleKey = `${keyPrefix}...[YOUR_FULL_KEY]`;
    const snippet = JSON.stringify({
      mcpServers: {
        "model-context-gateway": {
          url: "http://10.0.0.10:8026/sse",
          type: "sse",
          trust: true,
          headers: {
            "X-App-Key": sampleKey
          }
        }
      }
    }, null, 2);
    navigator.clipboard.writeText(snippet);
    showToast('Copied sample mcp_config.json snippet to clipboard!', 'success');
  };

  return (
    <div className="glass-card dcr-card">
      <div className="card-header-btn">
        <div>
          <h2>
            <i className="fa-solid fa-key"></i> {isAdmin ? 'App Keys' : 'My App Keys'}
          </h2>
          {limits && (
            <small style={{ color: 'var(--secondary)', display: 'block', marginTop: '2px' }}>
              {isAdmin ? (
                limits.userMax > 0 ? (
                  <>User Quota: <strong>{limits.userActiveKeys} / {limits.userMax}</strong> Keys Used &bull; Global: {limits.globalMax > 0 ? `${limits.totalActiveKeys} / ${limits.globalMax}` : 'Unlimited'}</>
                ) : (
                  <>Active Keys: <strong>{limits.userActiveKeys}</strong> &bull; Global: {limits.globalMax > 0 ? `${limits.totalActiveKeys} / ${limits.globalMax}` : 'Unlimited'} &bull; Quota: Unlimited</>
                )
              ) : (
                limits.userMax > 0 ? (
                  <>Personal Quota: <strong>{limits.userActiveKeys} / {limits.userMax}</strong> Keys Used</>
                ) : (
                  <>Quota: Unlimited &bull; Active Keys: <strong>{limits.userActiveKeys}</strong></>
                )
              )}
            </small>
          )}
        </div>
        <button className="btn btn-primary btn-sm" onClick={openModal} disabled={!!limits?.isLimitReached}>
          <i className="fa-solid fa-plus"></i> Create App Key
        </button>
      </div>

      {isAdmin && (
        <div className="sub-tabs-nav" style={{ display: 'flex', justifyContent: 'center', flexWrap: 'wrap', gap: '8px', marginBottom: '16px', borderBottom: '1px solid rgba(255,255,255,0.08)', paddingBottom: '10px' }}>
          <button
            className={`tab-btn btn-sm ${activeTab === 'personal' ? 'active' : ''}`}
            onClick={() => handleTabSwitch('personal')}
            style={{ borderRadius: '6px', padding: '6px 12px' }}
          >
            <i className="fa-solid fa-user"></i> User Personal Keys
          </button>
          <button
            className={`tab-btn btn-sm ${activeTab === 'system' ? 'active' : ''}`}
            onClick={() => handleTabSwitch('system')}
            style={{ borderRadius: '6px', padding: '6px 12px' }}
          >
            <i className="fa-solid fa-server"></i> App-Level Keys (System &amp; Integrations)
          </button>
          <button
            className={`tab-btn btn-sm ${activeTab === 'quotas' ? 'active' : ''}`}
            onClick={() => handleTabSwitch('quotas')}
            style={{ borderRadius: '6px', padding: '6px 12px' }}
          >
            <i className="fa-solid fa-sliders"></i> Custom User Quotas
          </button>
        </div>
      )}

      {isAdmin && activeTab === 'quotas' ? (
        <div className="custom-quotas-section">
          <p style={{ fontSize: '13px', color: 'var(--secondary)', marginBottom: '14px' }}>
            Set custom per-user App Key limits. Custom quotas override the default global user quota limit.
          </p>

          <form onSubmit={handleSaveQuota} style={{ display: 'flex', gap: '12px', alignItems: 'flex-end', marginBottom: '20px', flexWrap: 'wrap', background: 'rgba(255,255,255,0.02)', padding: '14px', borderRadius: '8px', border: '1px solid rgba(255,255,255,0.05)' }}>
            <div style={{ flex: '1', minWidth: '180px' }}>
              <label htmlFor="quota-username-input" style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>Username</label>
              <input
                id="quota-username-input"
                type="text"
                placeholder="e.g. jdoe"
                value={quotaUsername}
                onChange={(e) => setQuotaUsername(e.target.value)}
                required
                style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', background: 'rgba(0,0,0,0.3)', color: '#fff', border: '1px solid var(--glass-border)' }}
              />
            </div>
            <div style={{ width: '130px' }}>
              <label htmlFor="quota-max-keys-input" style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block', marginBottom: '4px' }}>Max Keys (0=unlimited)</label>
              <input
                id="quota-max-keys-input"
                type="number"
                min="0"
                value={quotaMaxKeys}
                onChange={(e) => setQuotaMaxKeys(parseInt(e.target.value, 10) || 0)}
                required
                style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', background: 'rgba(0,0,0,0.3)', color: '#fff', border: '1px solid var(--glass-border)' }}
              />
            </div>
            <button type="submit" className="btn btn-primary btn-sm" disabled={isSubmittingQuota}>
              <i className="fa-solid fa-plus"></i> Set Quota
            </button>
          </form>

          <div className="table-container">
            <table id="user-quotas-table">
              <thead>
                <tr>
                  <th>Username</th>
                  <th>Custom Quota</th>
                  <th>Updated</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {userQuotas.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="empty-state">
                      No custom user quotas configured. All users follow the default quota.
                    </td>
                  </tr>
                ) : (
                  userQuotas.map((q) => (
                    <tr key={q.username}>
                      <td><strong>{q.username}</strong></td>
                      <td>
                        <span className="server-badge" style={{ background: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8' }}>
                          {q.maxKeys === 0 ? 'Unlimited (0)' : `${q.maxKeys} keys`}
                        </span>
                      </td>
                      <td>{new Date(q.updatedAt || q.createdAt).toLocaleDateString()}</td>
                      <td>
                        <button
                          className="btn btn-danger btn-sm"
                          onClick={() => deleteUserQuota(q.username)}
                          title="Reset Quota to Default"
                        >
                          <i className="fa-solid fa-rotate-left"></i> Reset
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <>
          {isAdmin && activeTab === 'personal' && (
            <form onSubmit={handleFilter} style={{ display: 'flex', gap: '8px', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap' }}>
              <input
                type="text"
                placeholder="Filter by username..."
                value={usernameFilter}
                onChange={(e) => setUsernameFilter(e.target.value)}
                style={{ maxWidth: '240px', padding: '6px 10px', borderRadius: '6px', background: 'rgba(0,0,0,0.3)', color: '#fff', border: '1px solid var(--glass-border)', fontSize: '13px' }}
              />
              <button type="submit" className="btn btn-secondary btn-sm">
                <i className="fa-solid fa-filter"></i> Filter
              </button>
              {usernameFilter && (
                <button type="button" className="btn btn-secondary btn-sm" onClick={handleClearFilter}>
                  Clear
                </button>
              )}
            </form>
          )}

          <div className="table-container">
            <table id="appkeys-table">
              <thead>
                <tr>
                  <th>Key Name</th>
                  <th>Prefix</th>
                  {isAdmin && <th>Owner</th>}
                  <th>Scopes</th>
                  <th>Expires</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {appKeys.length === 0 ? (
                  <tr>
                    <td colSpan={isAdmin ? 7 : 6} className="empty-state">
                      {activeTab === 'system'
                        ? 'No app-level system keys active. Click "+ Create App Key" to generate a credential.'
                        : 'No App Keys active. Click "+ Create App Key" to generate a credential for CLI or IDE tools.'}
                    </td>
                  </tr>
                ) : (
                  appKeys.map((key) => (
                    <tr key={key.id}>
                      <td><strong>{key.name}</strong></td>
                      <td>
                        <code style={{ fontFamily: 'JetBrains Mono, monospace', fontSize: '11px', background: 'rgba(255,255,255,0.05)', padding: '2px 6px', borderRadius: '4px', color: 'var(--accent)' }}>
                          {key.keyPrefix}...
                        </code>
                      </td>
                      {isAdmin && <td>{key.username}</td>}
                      <td>
                        {key.scopes && key.scopes.length > 0 ? (
                          key.scopes.map((s, idx) => (
                            <span key={idx} className="server-badge" style={{ background: 'rgba(249, 115, 22, 0.1)', color: 'var(--accent)', marginRight: '4px' }}>
                              {s}
                            </span>
                          ))
                        ) : (
                          <span className="server-badge">all</span>
                        )}
                      </td>
                      <td>
                        {key.expiresAt ? (
                          new Date(key.expiresAt) < new Date() ? (
                            <span style={{ color: '#ef4444', fontWeight: 600 }}>Expired</span>
                          ) : (
                            <span>{new Date(key.expiresAt).toLocaleDateString()}</span>
                          )
                        ) : (
                          <span style={{ color: 'var(--secondary)' }}>Never</span>
                        )}
                      </td>
                      <td>{new Date(key.createdAt).toLocaleDateString()}</td>
                      <td>
                        <button
                          className="btn btn-secondary btn-sm"
                          onClick={() => copyConfigSnippet(key.keyPrefix)}
                          title="Copy MCP Config Snippet"
                          style={{ marginRight: '6px' }}
                        >
                          <i className="fa-solid fa-code"></i> Config
                        </button>
                        <button
                          className="btn btn-danger btn-sm"
                          onClick={() => revokeAppKey(key.id, key.name)}
                          title="Revoke Key"
                        >
                          <i className="fa-solid fa-trash"></i> Revoke
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
};
