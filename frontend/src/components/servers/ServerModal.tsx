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
    if (alias.trim()) {
      serverPayload.alias = alias.trim();
    }
    if (editingServer) {
      serverPayload.id = editingServer.id;
    }
    if (apiKey) {
      serverPayload.apiKey = apiKey;
    }

    try {
      await saveServer(serverPayload);
    } catch {
      // Error is handled upstream or ignored
    }
  };

  const showCustomHeaderName = authShape === 'custom-header' || authShape === 'query';

  return (
    <div className="modal-backdrop" id="server-modal" style={{ display: 'flex' }}>
      <div className="glass-card modal-card">
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-server"></i> {editingServer ? 'Edit MCP Server' : 'Add MCP Server'}
          </h2>
          <button className="btn-close" onClick={closeAddEditModal}>
            &times;
          </button>
        </div>
        <form onSubmit={handleSubmit} noValidate>
          <div className="form-group">
            <label htmlFor="server-name">Display Name</label>
            <input
              type="text"
              id="server-name"
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
                value={secretProvider}
                onChange={(e) => setSecretProvider(e.target.value)}
              >
                <option value="None">None (Static API Token)</option>
                <option value="Vault">HashiCorp Vault (KV v2)</option>
                <option value="WindowsRegistry">Windows Registry (DPAPI)</option>
                <option value="Environment">Environment Variables</option>
                <option value="UserProvided">User-Provided Authentication (PAT / Per-User)</option>
              </select>
            </div>
            <div className="form-group">
              <label htmlFor="server-secret-key">Secret Key / Item Name</label>
              <input
                type="text"
                id="server-secret-key"
                placeholder={secretProvider === 'Vault' ? 'e.g. secret:services/my-service:token' : 'e.g. NOTES_API_KEY'}
                value={secretKey}
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
              <strong>Active Directory Impersonation:</strong> Outbound requests pass the caller's Windows identity via Kerberos S4U2Proxy. Requires AD Constrained Delegation and SPNs registered.
            </div>
          )}

          <div className="form-group">
            <label htmlFor="server-key">Static API Token / Secret (Fallback)</label>
            <input
              type="password"
              id="server-key"
              placeholder="Fallback API token if secret provider is not used"
              value={apiKey}
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
            <div className="checkbox-group" style={{ marginBottom: "10px" }}>
              <label className="switch">
                <input
                  type="checkbox"
                  checked={allowPassThroughAuth}
                  onChange={(e) => setAllowPassThroughAuth(e.target.checked)}
                />
                <span className="slider"></span>
              </label>
              <span className="checkbox-label">Allow Dynamic Pass-Through Auth</span>
            </div>
            {allowPassThroughAuth && (
              <div>
                <label>Dynamic Auth Prompt Instructions</label>
                <input
                  type="text"
                  placeholder="e.g. Provide a JWT token in target_auth_token parameter"
                  value={dynamicAuthPrompt}
                  onChange={(e) => setDynamicAuthPrompt(e.target.value)}
                />
              </div>
            )}
          </div>

          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" onClick={closeAddEditModal}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" id="btn-save">
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
