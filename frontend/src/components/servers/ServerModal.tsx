import React, { useState } from 'react';
import { useServerStore } from '../../stores/useServerStore';
import { McpServer, ServerPayload } from '../../shared/types';

export interface ServerModalProps {
  isOpen?: boolean;
  onClose?: () => void;
  onSave?: (server: ServerPayload) => Promise<void> | void;
  server?: McpServer | null;
}

interface ServerModalDialogProps {
  editingServer?: McpServer | null;
  onClose?: () => void;
  onSave?: (server: ServerPayload) => Promise<void> | void;
}

const ServerModalDialog: React.FC<ServerModalDialogProps> = ({
  editingServer: propEditingServer,
  onClose: propOnClose,
  onSave: propOnSave,
}) => {
  const store = useServerStore();
  const editingServer = propEditingServer !== undefined ? propEditingServer : store.editingServer;
  const closeAddEditModal = propOnClose || store.closeAddEditModal;
  const saveServer = propOnSave || store.saveServer;

  const [displayName, setDisplayName] = useState(editingServer?.displayName || '');
  const [alias, setAlias] = useState(editingServer?.alias || '');
  const [aliasError, setAliasError] = useState('');
  const [type, setType] = useState(editingServer?.type || 'sse');
  const [category, setCategory] = useState(
    editingServer?.categories ? editingServer.categories.join(', ') : (editingServer ? 'default' : 'infrastructure')
  );
  const [url, setUrl] = useState(editingServer?.url || '');
  const [secretProvider, setSecretProvider] = useState(editingServer?.secretProvider || 'None');
  const [secretKey, setSecretKey] = useState(editingServer?.secretItemKey || '');
  const [authShape, setAuthShape] = useState(editingServer?.authShape || 'bearer');
  const [customHeaderName, setCustomHeaderName] = useState(editingServer?.customHeaderName || '');
  const [apiKey, setApiKey] = useState('');
  const [enabled, setEnabled] = useState(editingServer ? editingServer.enabled : true);
  const [hidden, setHidden] = useState(editingServer ? editingServer.hidden : false);
  const [allowPassThroughAuth, setAllowPassThroughAuth] = useState(editingServer ? editingServer.allowPassThroughAuth : false);
  const [dynamicAuthPrompt, setDynamicAuthPrompt] = useState(editingServer?.dynamicAuthPrompt || "");
  const [enableOAuth3Lo, setEnableOAuth3Lo] = useState(editingServer?.enableOAuth3Lo || false);
  const [oauthClientId, setOauthClientId] = useState(editingServer?.oauthClientId || (editingServer as any)?.oAuthClientId || '');
  const [oauthClientSecret, setOauthClientSecret] = useState('');
  const [oauthAuthorizationUrl, setOauthAuthorizationUrl] = useState(editingServer?.oauthAuthorizationUrl || (editingServer as any)?.oAuthAuthorizationUrl || '');
  const [oauthTokenUrl, setOauthTokenUrl] = useState(editingServer?.oauthTokenUrl || (editingServer as any)?.oAuthTokenUrl || '');
  const [oauthScopes, setOauthScopes] = useState(editingServer?.oauthScopes || (editingServer as any)?.oAuthScopes || '');
  const [oauthRedirectUri, setOauthRedirectUri] = useState(editingServer?.oauthRedirectUri || (editingServer as any)?.oAuthRedirectUri || '');

  const ALIAS_REGEX = /^[a-zA-Z0-9_-]*$/;

  const handleAliasChange = (val: string) => {
    setAlias(val);
    if (val && !ALIAS_REGEX.test(val)) {
      setAliasError('Alias can only contain letters, numbers, underscores, and hyphens');
    } else {
      setAliasError('');
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (alias && !ALIAS_REGEX.test(alias)) {
      setAliasError('Alias can only contain letters, numbers, underscores, and hyphens');
      return;
    }

    const serverPayload: ServerPayload = {
      displayName,
      type,
      categories: category.split(',').map((s) => s.trim()).filter(Boolean),
      url,
      secretProvider,
      secretItemKey: secretKey,
      authShape,
      customHeaderName,
      enabled,
      hidden,
      allowPassThroughAuth,
      dynamicAuthPrompt,
    };
    if (enableOAuth3Lo) {
      serverPayload.enableOAuth3Lo = true;
      if (oauthClientId.trim()) {
        serverPayload.oauthClientId = oauthClientId.trim();
      }
      if (oauthAuthorizationUrl.trim()) {
        serverPayload.oauthAuthorizationUrl = oauthAuthorizationUrl.trim();
      }
      if (oauthTokenUrl.trim()) {
        serverPayload.oauthTokenUrl = oauthTokenUrl.trim();
      }
      if (oauthScopes.trim()) {
        serverPayload.oauthScopes = oauthScopes.trim();
      }
      if (oauthRedirectUri.trim()) {
        serverPayload.oauthRedirectUri = oauthRedirectUri.trim();
      }
    } else if (editingServer?.enableOAuth3Lo) {
      serverPayload.enableOAuth3Lo = false;
    }
    if (alias.trim()) {
      serverPayload.alias = alias.trim();
    }
    if (editingServer) {
      serverPayload.id = editingServer.id;
    }
    if (apiKey) {
      serverPayload.apiKey = apiKey;
    }
    if (oauthClientSecret) {
      serverPayload.oauthClientSecret = oauthClientSecret;
    }

    try {
      await saveServer(serverPayload);
    } catch {
      // Error is handled upstream or ignored
    }
  };

  const showCustomHeaderName = authShape === 'custom-header' || authShape === 'query';

  return (
    <div className="modal-backdrop" id="server-modal" data-testid="server-modal" style={{ display: 'flex' }}>
      <div className="glass-card modal-card" style={{ maxWidth: '600px', width: '90%' }}>
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-server"></i> {editingServer ? 'Edit MCP Server' : 'Add MCP Server'}
          </h2>
          <button className="btn-close" data-testid="server-close-btn" onClick={closeAddEditModal} aria-label="Close modal">
            &times;
          </button>
        </div>
        <form onSubmit={handleSubmit} noValidate>
          <div className="form-group">
            <label htmlFor="server-name">Display Name</label>
            <input
              type="text"
              id="server-name"
              data-testid="server-name-input"
              aria-label="Display Name"
              placeholder="e.g. Notes RAG"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="server-alias">Alias / Namespace (Optional)</label>
            <input
              type="text"
              id="server-alias"
              data-testid="server-alias-input"
              aria-label="Alias or Namespace"
              placeholder="e.g. homebox_db"
              value={alias}
              onChange={(e) => handleAliasChange(e.target.value)}
              aria-invalid={!!aliasError}
            />
            {aliasError && (
              <div className="field-error" style={{ color: 'var(--danger-color, #ef4444)', fontSize: '0.8rem', marginTop: '4px' }}>
                {aliasError}
              </div>
            )}
            <small style={{ display: 'block', color: 'var(--text-muted)', fontSize: '0.8rem', marginTop: '4px' }}>
              Routing namespace (e.g. homebox_db). If omitted, the Server ID is used.
            </small>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="server-type">Transport Type</label>
              <select
                id="server-type"
                data-testid="server-type-select"
                value={type}
                onChange={(e) => setType(e.target.value)}
                required
              >
                <option value="sse">SSE</option>
                <option value="http">HTTP</option>
                <option value="streamable">Streamable HTTP</option>
                <option value="stdio">STDIO</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="server-category">Category</label>
              <input
                type="text"
                id="server-category"
                data-testid="server-category-input"
                aria-label="Category"
                placeholder="e.g. infrastructure"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="server-url">
              {type === 'stdio' ? 'Connection Command' : 'Connection URL'}
            </label>
            <input
              type="text"
              id="server-url"
              data-testid="server-url-input"
              aria-label={type === 'stdio' ? 'Connection Command' : 'Connection URL'}
              placeholder={type === 'stdio' ? 'e.g. node /app/mock_stdio.js' : 'e.g. http://notes-rag-mcp:3000/sse'}
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              required
            />
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="server-secret-provider">Secret Provider</label>
              <select
                id="server-secret-provider"
                data-testid="server-secret-provider-select"
                value={authShape === 'impersonation' ? 'None' : secretProvider}
                disabled={authShape === 'impersonation'}
                onChange={(e) => setSecretProvider(e.target.value)}
              >
                <option value="None">{authShape === 'impersonation' ? 'None (Windows Kerberos Identity)' : 'None (Static API Token)'}</option>
                <option value="Vault">HashiCorp Vault (KV v2)</option>
                <option value="WindowsRegistry">Windows Registry (DPAPI)</option>
                <option value="Environment">Environment Variables</option>
                <option value="TokenExchange">OAuth2 / OIDC Token Exchange (RFC 8693)</option>
                <option value="UserProvided">User-Provided Authentication (PAT / Per-User)</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="server-secret-key">Secret Key / Item Name</label>
              <input
                type="text"
                id="server-secret-key"
                data-testid="server-secret-key-input"
                aria-label="Secret Key or Item Name"
                disabled={authShape === 'impersonation'}
                placeholder={
                  authShape === 'impersonation'
                    ? 'Not applicable for Kerberos Impersonation'
                    : secretProvider === 'Vault'
                    ? 'e.g. secret:services/my-service:token or acme/mcgateway/steve/slack'
                    : secretProvider === 'TokenExchange'
                    ? 'e.g. downstream-service-audience or resource-uri'
                    : secretProvider === 'UserProvided'
                    ? 'Optional: JSON subkey (e.g. token, access_token) or blank'
                    : 'e.g. NOTES_API_KEY'
                }
                value={authShape === 'impersonation' ? '' : secretKey}
                onChange={(e) => setSecretKey(e.target.value)}
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="server-auth-shape">Auth Token Format / Shape</label>
              <select
                id="server-auth-shape"
                value={authShape}
                onChange={(e) => setAuthShape(e.target.value)}
              >
                <option value="bearer">Bearer Token (Authorization: Bearer &lt;token&gt;)</option>
                <option value="basic">Basic Auth (Authorization: Basic &lt;token&gt;)</option>
                <option value="raw">Raw Auth Header (Authorization: &lt;token&gt;)</option>
                <option value="x-api-key">X-API-Key Header (X-API-Key: &lt;token&gt;)</option>
                <option value="custom-header">Custom Header Name (e.g. Slack-Token: &lt;token&gt;)</option>
                <option value="query">URL Query Parameter (e.g. ?token=&lt;token&gt;)</option>
                <option value="impersonation">Kerberos / NTLM Impersonation (S4U2Proxy / RunImpersonated)</option>
              </select>
            </div>
            {showCustomHeaderName && (
              <div className="form-group" id="group-custom-header-name">
                <label htmlFor="server-custom-header-name">Custom Header / Query Name</label>
                <input
                  type="text"
                  id="server-custom-header-name"
                  aria-label="Custom Header or Query Name"
                  placeholder="e.g. Slack-Bot-Token or token"
                  value={customHeaderName}
                  onChange={(e) => setCustomHeaderName(e.target.value)}
                />
              </div>
            )}
          </div>

          {authShape === 'impersonation' && (
            <div className="form-group" style={{ marginBottom: '12px', padding: '10px 12px', background: 'rgba(234, 179, 8, 0.1)', border: '1px solid rgba(234, 179, 8, 0.3)', borderRadius: '6px', fontSize: '0.85rem' }}>
              <i className="fa-solid fa-triangle-exclamation" style={{ color: '#eab308', marginRight: '6px' }}></i>
              <strong>Active Directory Impersonation:</strong> Outbound requests pass the caller's Windows identity via Kerberos S4U2Proxy. Secret providers and static tokens are bypassed.
            </div>
          )}

          {secretProvider === 'UserProvided' && authShape !== 'impersonation' && (
            <div className="form-group" style={{ marginBottom: '12px', padding: '10px 12px', background: 'rgba(59, 130, 246, 0.1)', border: '1px solid rgba(59, 130, 246, 0.3)', borderRadius: '6px', fontSize: '0.85rem' }}>
              <i className="fa-solid fa-user-lock" style={{ color: '#3b82f6', marginRight: '6px' }}></i>
              <strong>User-Provided Authentication (BYOK):</strong> Credentials are resolved individually per authenticated user from their personal secret store (Database or HashiCorp Vault). Users manage their personal tokens in the <strong>My MCP Servers</strong> tab.
            </div>
          )}

          {secretProvider === 'TokenExchange' && authShape !== 'impersonation' && (
            <div className="form-group" style={{ marginBottom: '12px', padding: '10px 12px', background: 'rgba(168, 85, 247, 0.1)', border: '1px solid rgba(168, 85, 247, 0.3)', borderRadius: '6px', fontSize: '0.85rem' }}>
              <i className="fa-solid fa-repeat" style={{ color: '#a855f7', marginRight: '6px' }}></i>
              <strong>RFC 8693 Token Exchange:</strong> The gateway exchanges the client's incoming JWT for a downstream scoped service token asserted on behalf of the user.
            </div>
          )}

          <div className="form-group">
            <label htmlFor="server-key">Static API Token / Secret (Fallback)</label>
            <input
              type="password"
              id="server-key"
              aria-label="Static API Token or Secret"
              disabled={authShape === 'impersonation' || secretProvider === 'UserProvided'}
              placeholder={
                authShape === 'impersonation'
                  ? 'Not applicable for Kerberos Impersonation'
                  : secretProvider === 'UserProvided'
                  ? 'Not applicable: resolved per user from Database or Vault'
                  : secretProvider === 'None'
                  ? 'Primary API token or secret'
                  : 'Fallback API token if secret provider is unavailable'
              }
              value={authShape === 'impersonation' || secretProvider === 'UserProvided' ? '' : apiKey}
              onChange={(e) => setApiKey(e.target.value)}
            />
          </div>

          <div className="form-row checkbox-row">
            <div className="checkbox-group">
              <label className="switch">
                <input
                  type="checkbox"
                  id="server-enabled"
                  checked={enabled}
                  onChange={(e) => setEnabled(e.target.checked)}
                />
                <span className="slider"></span>
              </label>
              <span className="checkbox-label">Enabled</span>
            </div>
            <div className="checkbox-group">
              <label className="switch">
                <input
                  type="checkbox"
                  id="server-hidden"
                  checked={hidden}
                  onChange={(e) => setHidden(e.target.checked)}
                />
                <span className="slider"></span>
              </label>
              <span className="checkbox-label">Hidden</span>
            </div>
          </div>

          <div className="form-group">
            <div className="checkbox-group" style={{ marginBottom: '10px' }}>
              <label className="switch">
                <input
                  type="checkbox"
                  id="server-allow-passthrough"
                  checked={allowPassThroughAuth}
                  onChange={(e) => setAllowPassThroughAuth(e.target.checked)}
                />
                <span className="slider"></span>
              </label>
              <span className="checkbox-label">Allow Dynamic Pass-Through Auth</span>
            </div>
            {allowPassThroughAuth && (
              <div className="form-group" style={{ marginTop: '10px', marginBottom: 0 }}>
                <label htmlFor="server-dynamic-auth-prompt">Dynamic Auth Prompt Instructions</label>
                <input
                  type="text"
                  id="server-dynamic-auth-prompt"
                  placeholder="e.g. Provide a JWT token in target_auth_token parameter"
                  value={dynamicAuthPrompt}
                  onChange={(e) => setDynamicAuthPrompt(e.target.value)}
                />
              </div>
            )}
          </div>

          <div className="form-group" style={{ marginTop: '16px', paddingTop: '14px', borderTop: '1px solid var(--border-color, rgba(255, 255, 255, 0.1))' }}>
            <div className="checkbox-group" style={{ marginBottom: '8px' }}>
              <label className="switch">
                <input
                  type="checkbox"
                  id="server-enable-oauth-3lo"
                  data-testid="server-enable-oauth-3lo"
                  checked={enableOAuth3Lo}
                  onChange={(e) => setEnableOAuth3Lo(e.target.checked)}
                />
                <span className="slider"></span>
              </label>
              <span className="checkbox-label">Enable 3LO User Delegation (OAuth 2.0)</span>
            </div>
            <small style={{ display: 'block', color: 'var(--text-muted)', fontSize: '0.8rem', marginBottom: enableOAuth3Lo ? '12px' : '0' }}>
              Allows interactive users to authorize with the backend server via OAuth 3LO redirect, delegating their user identity while falling back to the static token for automated agents.
            </small>

            {enableOAuth3Lo && (
              <div className="oauth-3lo-config" style={{ background: 'rgba(59, 130, 246, 0.05)', border: '1px solid rgba(59, 130, 246, 0.2)', borderRadius: '6px', padding: '12px', marginTop: '10px' }}>
                <div className="form-row">
                  <div className="form-group">
                    <label htmlFor="server-oauth-client-id">OAuth Client ID</label>
                    <input
                      type="text"
                      id="server-oauth-client-id"
                      data-testid="server-oauth-client-id-input"
                      placeholder="e.g. 11145550917233.12161096706727"
                      value={oauthClientId}
                      onChange={(e) => setOauthClientId(e.target.value)}
                    />
                  </div>
                  <div className="form-group">
                    <label htmlFor="server-oauth-client-secret">
                      OAuth Client Secret {editingServer?.id && '(leave blank to keep unchanged)'}
                    </label>
                    <input
                      type="password"
                      id="server-oauth-client-secret"
                      data-testid="server-oauth-client-secret-input"
                      placeholder={editingServer?.id ? '••••••••' : 'Client secret from OAuth provider'}
                      value={oauthClientSecret}
                      onChange={(e) => setOauthClientSecret(e.target.value)}
                    />
                  </div>
                </div>

                <div className="form-group">
                  <label htmlFor="server-oauth-auth-url">Authorization URL</label>
                  <input
                    type="text"
                    id="server-oauth-auth-url"
                    data-testid="server-oauth-auth-url-input"
                    placeholder="e.g. https://slack.com/oauth/v2_user/authorize"
                    value={oauthAuthorizationUrl}
                    onChange={(e) => setOauthAuthorizationUrl(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="server-oauth-token-url">Token Exchange URL</label>
                  <input
                    type="text"
                    id="server-oauth-token-url"
                    data-testid="server-oauth-token-url-input"
                    placeholder="e.g. https://slack.com/api/oauth.v2.user.access"
                    value={oauthTokenUrl}
                    onChange={(e) => setOauthTokenUrl(e.target.value)}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="server-oauth-scopes">OAuth Scopes (Space-separated)</label>
                  <input
                    type="text"
                    id="server-oauth-scopes"
                    data-testid="server-oauth-scopes-input"
                    placeholder="e.g. channels:read chat:write search:read"
                    value={oauthScopes}
                    onChange={(e) => setOauthScopes(e.target.value)}
                  />
                </div>

                <div className="form-group" style={{ marginBottom: 0 }}>
                  <label htmlFor="server-oauth-redirect-uri">OAuth Redirect URI</label>
                  <input
                    type="text"
                    id="server-oauth-redirect-uri"
                    data-testid="server-oauth-redirect-uri-input"
                    placeholder="e.g. https://mcp.wileyriley.com/api/oauth/egress/callback"
                    value={oauthRedirectUri}
                    onChange={(e) => setOauthRedirectUri(e.target.value)}
                  />
                </div>
              </div>
            )}
          </div>

          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" data-testid="server-cancel-btn" onClick={closeAddEditModal}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" id="btn-save" data-testid="server-save-btn">
              Save Server
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export const ServerModal: React.FC<ServerModalProps> = ({
  isOpen,
  onClose,
  onSave,
  server,
}) => {
  const store = useServerStore();
  const showModal = isOpen !== undefined ? isOpen : store.isAddEditOpen;
  const editingServer = server !== undefined ? server : store.editingServer;
  const handleClose = onClose || store.closeAddEditModal;
  const handleSave = onSave || store.saveServer;

  if (!showModal) return null;

  return (
    <ServerModalDialog
      key={editingServer?.id || 'new'}
      editingServer={editingServer}
      onClose={handleClose}
      onSave={handleSave}
    />
  );
};
