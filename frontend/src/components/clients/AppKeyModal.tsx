import React, { useState } from 'react';
import { useAppKeyStore } from '../../stores/useAppKeyStore';
import { useUserStore } from '../../stores/useUserStore';

export const AppKeyModal: React.FC = () => {
  const { user } = useUserStore();
  const isAdmin = !!(user?.groups && user.groups.includes('full_admin'));

  const {
    isCreateModalOpen,
    createdResult,
    createAppKey,
    closeModal,
    limits,
    keyTypeTab
  } = useAppKeyStore();

  const [name, setName] = useState('');
  const [keyType, setKeyType] = useState<'personal' | 'system'>(() => keyTypeTab === 'system' ? 'system' : 'personal');
  const [targetUsername, setTargetUsername] = useState('');
  const [scopeType, setScopeType] = useState<'all' | 'server' | 'category'>('all');
  const [customScope, setCustomScope] = useState('');
  const [expiresInDays, setExpiresInDays] = useState<number | undefined>(undefined);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [copiedKey, setCopiedKey] = useState(false);

  if (!isCreateModalOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    let scopes = ['all'];
    if (scopeType === 'server' && customScope.trim()) {
      scopes = [`server:${customScope.trim()}`];
    } else if (scopeType === 'category' && customScope.trim()) {
      scopes = [`category:${customScope.trim()}`];
    }

    try {
      await createAppKey({
        name,
        keyType: isAdmin ? keyType : 'personal',
        username: isAdmin && keyType === 'personal' && targetUsername.trim() ? targetUsername.trim() : undefined,
        scopes,
        expiresInDays
      });
    } catch (err) {
      console.error(err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const copyPlaintextKey = () => {
    if (createdResult?.plaintextKey) {
      navigator.clipboard.writeText(createdResult.plaintextKey);
      setCopiedKey(true);
      setTimeout(() => setCopiedKey(false), 2000);
    }
  };

  const getMcpConfigSnippet = () => {
    if (!createdResult?.plaintextKey) return '';
    return JSON.stringify({
      mcpServers: {
        "model-context-gateway": {
          url: "http://10.0.0.10:8026/sse",
          type: "sse",
          trust: true,
          headers: {
            "X-App-Key": createdResult.plaintextKey
          }
        }
      }
    }, null, 2);
  };

  return (
    <div id="add-appkey-modal" data-testid="appkey-modal" className="modal-backdrop" style={{ display: 'flex' }}>
      <div className="glass-card modal-card" style={{ maxWidth: '540px' }}>
        <div className="modal-header">
          <h2><i className="fa-solid fa-key"></i> {isAdmin ? 'Create New App Key' : 'Create Personal App Key'}</h2>
          <button className="btn-close" data-testid="appkey-close-btn" onClick={closeModal}>&times;</button>
        </div>

        {!createdResult ? (
          <form onSubmit={handleSubmit}>
            {isAdmin ? (
              <>
                <div className="form-group">
                  <label htmlFor="modal-key-type">Key Type</label>
                  <select
                    id="modal-key-type"
                    data-testid="appkey-keytype-select"
                    value={keyType}
                    onChange={(e) => setKeyType(e.target.value as 'personal' | 'system')}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', background: 'rgba(0,0,0,0.3)', color: '#fff', border: '1px solid var(--glass-border)' }}
                  >
                    <option value="personal">Personal Key (User-owned)</option>
                    <option value="system">App-Level / System Key (Service &amp; Integration)</option>
                  </select>
                </div>

                {keyType === 'personal' && (
                  <div className="form-group">
                    <label htmlFor="modal-target-username">Target Username (optional, defaults to current user)</label>
                    <input
                      type="text"
                      id="modal-target-username"
                      data-testid="appkey-target-username-input"
                      placeholder={user?.username || 'Username'}
                      value={targetUsername}
                      onChange={(e) => setTargetUsername(e.target.value)}
                    />
                  </div>
                )}
              </>
            ) : (
              <div className="form-group" style={{ background: 'rgba(255,255,255,0.03)', padding: '12px', borderRadius: '6px', border: '1px solid rgba(255,255,255,0.08)', marginBottom: '16px' }}>
                <div style={{ fontSize: '13px', color: '#e2e8f0' }}>
                  Key Type: <strong style={{ color: 'var(--accent)' }}>Personal Key</strong> ({user?.username || 'Current User'})
                </div>
                {limits && (
                  <div style={{ fontSize: '12px', marginTop: '6px', color: limits.isLimitReached ? '#ef4444' : 'var(--secondary)' }}>
                    {limits.userMax > 0 ? (
                      <>Remaining Quota: <strong>{Math.max(0, limits.userMax - limits.userActiveKeys)}</strong> keys left ({limits.userActiveKeys} / {limits.userMax} used)</>
                    ) : (
                      <>Quota: <strong>Unlimited</strong> &bull; Active: {limits.userActiveKeys}</>
                    )}
                  </div>
                )}
              </div>
            )}

            <div className="form-group">
              <label htmlFor="key-name">Key Name (e.g. Cursor IDE, OpenClaw Agent)</label>
              <input
                type="text"
                id="key-name"
                data-testid="appkey-name-input"
                placeholder="e.g. My Laptop CLI"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="key-scope-type">Scope / Access Level</label>
              <select
                id="key-scope-type"
                data-testid="appkey-scope-select"
                value={scopeType}
                onChange={(e) => setScopeType(e.target.value as any)}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', background: 'rgba(0,0,0,0.3)', color: '#fff', border: '1px solid var(--glass-border)' }}
              >
                <option value="all">Full Gateway Access (all)</option>
                <option value="server">Server Scope (server:&lt;name&gt;)</option>
                <option value="category">Category Scope (category:&lt;name&gt;)</option>
              </select>
            </div>

            {scopeType !== 'all' && (
              <div className="form-group">
                <label htmlFor="key-custom-scope">Target Server / Category Name</label>
                <input
                  type="text"
                  id="key-custom-scope"
                  data-testid="appkey-custom-scope-input"
                  placeholder={scopeType === 'server' ? 'e.g. ha, docker' : 'e.g. smarthome, media'}
                  value={customScope}
                  onChange={(e) => setCustomScope(e.target.value)}
                  required
                />
              </div>
            )}

            <div className="form-group">
              <label htmlFor="key-expires">Expiration</label>
              <select
                id="key-expires"
                data-testid="appkey-expires-select"
                value={expiresInDays === undefined ? 'never' : expiresInDays}
                onChange={(e) => setExpiresInDays(e.target.value === 'never' ? undefined : Number(e.target.value))}
                style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', background: 'rgba(0,0,0,0.3)', color: '#fff', border: '1px solid var(--glass-border)' }}
              >
                <option value="never">Never (No expiration)</option>
                <option value="30">30 Days</option>
                <option value="90">90 Days</option>
                <option value="365">1 Year (365 Days)</option>
              </select>
            </div>

            <div className="modal-footer">
              <button type="button" className="btn btn-secondary" data-testid="appkey-cancel-btn" onClick={closeModal}>Cancel</button>
              <button type="submit" className="btn btn-primary" data-testid="appkey-save-btn" disabled={isSubmitting || !!limits?.isLimitReached}>
                {isSubmitting ? 'Generating...' : 'Generate App Key'}
              </button>
            </div>
          </form>
        ) : (
          <div data-testid="appkey-created-result" style={{ padding: '10px', background: 'rgba(249, 115, 22, 0.08)', border: '1px solid var(--accent)', borderRadius: '8px' }}>
            <h4 style={{ color: 'var(--accent)', margin: '0 0 10px 0' }}><i className="fa-solid fa-check-circle"></i> App Key Created!</h4>
            <p style={{ fontSize: '13px', margin: '4px 0 12px 0', color: 'var(--secondary)' }}>
              Copy your App Key now. It will <strong>never be shown again</strong>.
            </p>

            <div style={{ background: '#090d16', padding: '10px 14px', borderRadius: '6px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '10px' }}>
              <code style={{ fontFamily: 'JetBrains Mono, monospace', fontSize: '12px', color: '#38bdf8', wordBreak: 'break-all' }}>
                {createdResult.plaintextKey}
              </code>
              <button type="button" className="btn btn-secondary btn-sm" onClick={copyPlaintextKey}>
                {copiedKey ? <i className="fa-solid fa-check"></i> : <i className="fa-solid fa-copy"></i>}
              </button>
            </div>

            <h5 style={{ margin: '14px 0 6px 0', fontSize: '12px', color: 'var(--secondary)' }}>Ready-to-Use mcp_config.json Snippet:</h5>
            <pre style={{ background: '#090d16', padding: '10px', borderRadius: '6px', fontSize: '11px', maxHeight: '140px', overflowY: 'auto', color: '#cbd5e1' }}>
              {getMcpConfigSnippet()}
            </pre>

            <button type="button" className="btn btn-primary" data-testid="appkey-done-btn" onClick={closeModal} style={{ marginTop: '14px', width: '100%' }}>
              Done
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
