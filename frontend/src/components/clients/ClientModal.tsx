import React, { useState } from 'react';
import { useClientStore } from '../../stores/useClientStore';
import { showToast } from '../../stores/useToastStore';

export const ClientModal: React.FC = () => {
  const { isAddClientOpen, createdClientResult, registerClient, closeClientModal } = useClientStore();

  const [clientName, setClientName] = useState('');
  const [clientType, setClientType] = useState<'confidential' | 'public'>('confidential');
  const [redirectUris, setRedirectUris] = useState('');
  const [grantTypes, setGrantTypes] = useState<string[]>([
    'authorization_code',
    'refresh_token',
    'client_credentials'
  ]);
  const [clientScopes, setClientScopes] = useState('');
  const [expiresInDays, setExpiresInDays] = useState<number | undefined>(undefined);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [copiedId, setCopiedId] = useState(false);
  const [copiedSecret, setCopiedSecret] = useState(false);

  if (!isAddClientOpen) return null;

  const handleGrantTypeToggle = (type: string) => {
    if (grantTypes.includes(type)) {
      setGrantTypes(grantTypes.filter((g) => g !== type));
    } else {
      setGrantTypes([...grantTypes, type]);
    }
  };

  const copyToClipboard = (text: string, label: string, isSecret = false) => {
    navigator.clipboard.writeText(text);
    if (isSecret) {
      setCopiedSecret(true);
      setTimeout(() => setCopiedSecret(false), 2000);
    } else {
      setCopiedId(true);
      setTimeout(() => setCopiedId(false), 2000);
    }
    showToast(`${label} copied to clipboard`, 'info');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    const scopes = clientScopes
      ? clientScopes.split(',').map((s) => s.trim()).filter((s) => s.length > 0)
      : [];

    const uris = redirectUris
      ? redirectUris.split(/[,\n]/).map((s) => s.trim()).filter((s) => s.length > 0)
      : undefined;

    try {
      await registerClient(
        clientName,
        scopes,
        uris,
        grantTypes.length > 0 ? grantTypes : undefined,
        clientType,
        expiresInDays
      );
    } catch (err) {
      console.error(err);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div id="add-client-modal" data-testid="client-modal" className="modal-backdrop" style={{ display: 'flex' }}>
      <div className="glass-card modal-card" style={{ maxWidth: '560px' }}>
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-desktop"></i> Register New Client
          </h2>
          <button className="btn-close" data-testid="client-close-btn" onClick={closeClientModal}>
            &times;
          </button>
        </div>

        {!createdClientResult ? (
          <form id="client-form" onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="client-name">Client Name (Display Name)</label>
              <input
                type="text"
                id="client-name"
                data-testid="client-name-input"
                placeholder="e.g. VSCode Extension, Postman Integration"
                value={clientName}
                onChange={(e) => setClientName(e.target.value)}
                required
              />
            </div>

            <div className="form-group">
              <label htmlFor="client-type">Client Type</label>
              <select
                id="client-type"
                data-testid="client-type-select"
                value={clientType}
                onChange={(e) => setClientType(e.target.value as 'confidential' | 'public')}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '6px',
                  background: 'rgba(0,0,0,0.3)',
                  color: '#fff',
                  border: '1px solid var(--glass-border)'
                }}
              >
                <option value="confidential">Confidential (Server / CLI / Agent)</option>
                <option value="public">Public (SPA / Mobile / Native App)</option>
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="client-redirect-uris">Redirect URIs (Comma-separated)</label>
              <input
                type="text"
                id="client-redirect-uris"
                data-testid="client-redirect-input"
                placeholder="e.g. https://oauth.pstmn.io/v1/callback, http://localhost:3000/callback"
                value={redirectUris}
                onChange={(e) => setRedirectUris(e.target.value)}
              />
              <small style={{ color: 'var(--secondary)', display: 'block', marginTop: '4px' }}>
                Comma-separated OAuth callback URIs. Default: none.
              </small>
            </div>

            <div className="form-group">
              <label style={{ marginBottom: '8px', display: 'block' }}>Grant Types</label>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', background: 'rgba(0,0,0,0.2)', padding: '10px', borderRadius: '6px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '13px' }}>
                  <input
                    type="checkbox"
                    checked={grantTypes.includes('authorization_code')}
                    onChange={() => handleGrantTypeToggle('authorization_code')}
                    aria-label="authorization_code"
                  />
                  <span>Authorization Code (<code>authorization_code</code>)</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '13px' }}>
                  <input
                    type="checkbox"
                    checked={grantTypes.includes('refresh_token')}
                    onChange={() => handleGrantTypeToggle('refresh_token')}
                    aria-label="refresh_token"
                  />
                  <span>Refresh Token (<code>refresh_token</code>)</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '13px' }}>
                  <input
                    type="checkbox"
                    checked={grantTypes.includes('client_credentials')}
                    onChange={() => handleGrantTypeToggle('client_credentials')}
                    aria-label="client_credentials"
                  />
                  <span>Client Credentials (<code>client_credentials</code>)</span>
                </label>
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="client-scopes">Roles / Scopes (Comma-separated)</label>
              <input
                type="text"
                id="client-scopes"
                data-testid="client-scopes-input"
                placeholder="e.g. mcp_client, admin, category:smarthome"
                value={clientScopes}
                onChange={(e) => setClientScopes(e.target.value)}
              />
              <small style={{ color: 'var(--secondary)', display: 'block', marginTop: '4px' }}>
                Leave blank for default 'mcp_client' scope. Comma-separated (e.g. openid, offline_access, category:name).
              </small>
            </div>

            <div className="form-group">
              <label htmlFor="client-expires">Expiration</label>
              <select
                id="client-expires"
                data-testid="client-expires-select"
                value={expiresInDays === undefined ? 'never' : expiresInDays}
                onChange={(e) => setExpiresInDays(e.target.value === 'never' ? undefined : Number(e.target.value))}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '6px',
                  background: 'rgba(0,0,0,0.3)',
                  color: '#fff',
                  border: '1px solid var(--glass-border)'
                }}
              >
                <option value="never">Never (No expiration)</option>
                <option value="30">30 Days</option>
                <option value="90">90 Days</option>
                <option value="365">1 Year (365 Days)</option>
              </select>
            </div>

            <div className="modal-footer">
              <button type="button" className="btn btn-secondary" data-testid="client-cancel-btn" onClick={closeClientModal}>
                Cancel
              </button>
              <button type="submit" className="btn btn-primary" id="btn-save-client" data-testid="client-save-btn" disabled={isSubmitting}>
                {isSubmitting ? 'Generating...' : 'Generate Client'}
              </button>
            </div>
          </form>
        ) : (
          <div
            id="client-secret-result"
            data-testid="client-secret-result"
            style={{
              padding: '14px',
              background: 'rgba(16, 185, 129, 0.1)',
              border: '1px solid var(--accent)',
              borderRadius: '8px',
            }}
          >
            <h4 style={{ color: 'var(--accent)', margin: '0 0 12px 0' }}>
              <i className="fa-solid fa-check-circle"></i> Client Created Successfully!
            </h4>

            <div style={{ marginBottom: '12px' }}>
              <strong style={{ fontSize: '13px', display: 'block', marginBottom: '4px' }}>Client ID:</strong>
              <div style={{ background: '#090d16', padding: '8px 12px', borderRadius: '6px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '8px' }}>
                <span className="code" style={{ wordBreak: 'break-all', fontSize: '12px', color: '#38bdf8' }}>
                  {createdClientResult.clientId}
                </span>
                <button
                  type="button"
                  aria-label="Copy Client ID"
                  className="btn btn-secondary btn-sm"
                  onClick={() => copyToClipboard(createdClientResult.clientId, 'Client ID')}
                  title="Copy Client ID"
                >
                  {copiedId ? <i className="fa-solid fa-check"></i> : <i className="fa-solid fa-copy"></i>} Copy
                </button>
              </div>
            </div>

            {createdClientResult.clientSecret && (
              <div style={{ marginBottom: '12px' }}>
                <strong style={{ fontSize: '13px', display: 'block', marginBottom: '4px' }}>Client Secret:</strong>
                <div style={{ background: '#090d16', padding: '8px 12px', borderRadius: '6px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '8px' }}>
                  <span className="code" style={{ wordBreak: 'break-all', fontSize: '12px', color: 'var(--accent)' }}>
                    {createdClientResult.clientSecret}
                  </span>
                  <button
                    type="button"
                    aria-label="Copy Client Secret"
                    className="btn btn-secondary btn-sm"
                    onClick={() => copyToClipboard(createdClientResult.clientSecret, 'Client Secret', true)}
                    title="Copy Client Secret"
                  >
                    {copiedSecret ? <i className="fa-solid fa-check"></i> : <i className="fa-solid fa-copy"></i>} Copy
                  </button>
                </div>
              </div>
            )}

            <p style={{ color: 'var(--secondary)', fontSize: '13px', display: 'flex', alignItems: 'center', gap: '5px', margin: '12px 0 0 0' }}>
              <i className="fa-solid fa-triangle-exclamation"></i> Save this secret now. It will not be shown again.
            </p>

            <button
              type="button"
              className="btn btn-secondary btn-sm"
              data-testid="client-done-btn"
              onClick={closeClientModal}
              style={{ marginTop: '15px', width: '100%' }}
            >
              Close
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
