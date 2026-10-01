import React, { useState, useEffect } from 'react';
import { RegisteredClient, useClientStore } from '../../stores/useClientStore';
import { showToast } from '../../stores/useToastStore';
import { formatRelativeTime } from '../../utils/dateUtils';
import { RedirectUrisModal } from './RedirectUrisModal';

export const RegisteredClientsCard: React.FC = () => {
  const { clients, fetchClients, deleteClient, cleanupClients, openAddClientModal, rotateClientSecret } = useClientStore();
  const [selectedClientForUris, setSelectedClientForUris] = useState<RegisteredClient | null>(null);

  useEffect(() => {
    fetchClients();
  }, [fetchClients]);

  const copyClientId = (clientId: string) => {
    navigator.clipboard.writeText(clientId);
    showToast('Client ID copied to clipboard', 'info');
  };

  return (
    <div className="glass-card dcr-card" data-testid="registered-clients-card">
      <div className="card-header-btn">
        <h2>
          <i className="fa-solid fa-desktop"></i> Dynamic Client Registration (RFC 7591)
        </h2>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button className="btn btn-secondary btn-sm" id="btn-cleanup-clients" data-testid="btn-cleanup-dcr" onClick={() => cleanupClients()} title="Prune duplicate and stale dynamic client registrations">
            <i className="fa-solid fa-broom"></i> Clean Up DCR
          </button>
          <button className="btn btn-primary btn-sm" id="btn-add-client" data-testid="btn-register-client" onClick={openAddClientModal}>
            <i className="fa-solid fa-plus"></i> Register Client
          </button>
        </div>
      </div>

      <div className="table-container">
        <table id="clients-table">
          <thead>
            <tr>
              <th>Application Name</th>
              <th>Client ID</th>
              <th>Type</th>
              <th>Grant Types</th>
              <th>Redirect URIs</th>
              <th>Scopes</th>
              <th>Created / Expires</th>
              <th>Last Used</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {clients.length === 0 ? (
              <tr>
                <td colSpan={9} className="empty-state">
                  No registered clients found.
                </td>
              </tr>
            ) : (
              clients.map((c) => (
                <tr key={c.id}>
                  <td>
                    <strong>{c.displayName}</strong>
                  </td>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span className="code">{c.clientId}</span>
                      <button
                        type="button"
                        aria-label="Copy Client ID"
                        className="btn btn-secondary btn-sm"
                        style={{ padding: '2px 6px', fontSize: '11px' }}
                        onClick={() => copyClientId(c.clientId)}
                        title="Copy Client ID"
                      >
                        <i className="fa-solid fa-copy"></i>
                      </button>
                    </div>
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap' }}>
                      <span className="server-badge" style={{ background: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8' }}>
                        {c.clientType || 'confidential'}
                      </span>
                      <span className="server-badge">
                        {c.isDynamic ? 'Dynamic' : 'Manual'}
                      </span>
                    </div>
                  </td>
                  <td>
                    <span className="server-badge" style={{ background: 'rgba(255, 255, 255, 0.05)' }}>
                      {c.grantTypes && c.grantTypes.length > 0
                        ? c.grantTypes.join(', ')
                        : 'client_credentials, authorization_code'}
                    </span>
                  </td>
                  <td>
                    {c.redirectUris && c.redirectUris.length > 0 ? (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap' }}>
                        <span
                          className="server-badge"
                          style={{
                            fontSize: '11px',
                            maxWidth: '180px',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                            display: 'inline-block',
                            verticalAlign: 'middle',
                            cursor: 'pointer',
                          }}
                          onClick={() => setSelectedClientForUris(c)}
                          title={`Click to view: ${c.redirectUris[0]}`}
                          data-testid={`badge-redirect-uri-${c.id}`}
                        >
                          {c.redirectUris[0]}
                        </span>
                        {c.redirectUris.length > 1 && (
                          <button
                            type="button"
                            className="badge badge-primary"
                            style={{
                              cursor: 'pointer',
                              border: 'none',
                              padding: '2px 6px',
                              fontSize: '11px',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '3px',
                            }}
                            onClick={() => setSelectedClientForUris(c)}
                            title={`View all ${c.redirectUris.length} redirect URIs`}
                            aria-label={`View all ${c.redirectUris.length} redirect URIs for ${c.displayName}`}
                            data-testid={`btn-view-uris-${c.id}`}
                          >
                            +{c.redirectUris.length - 1} more
                          </button>
                        )}
                      </div>
                    ) : (
                      <span style={{ color: 'var(--secondary)' }}>&mdash;</span>
                    )}
                  </td>
                  <td>
                    <span className="server-badge">
                      {c.scopes && c.scopes.length > 0 ? c.scopes.join(', ') : 'mcp_client'}
                    </span>
                  </td>
                  <td>
                    <div style={{ fontSize: '12px', display: 'flex', flexDirection: 'column', gap: '2px' }}>
                      <span>{c.createdAt ? new Date(c.createdAt).toLocaleDateString() : '&mdash;'}</span>
                      <small style={{ color: c.expiresAt && new Date(c.expiresAt) < new Date() ? '#ef4444' : 'var(--secondary)' }}>
                        {c.expiresAt ? (
                          new Date(c.expiresAt) < new Date() ? 'Expired' : `Exp: ${new Date(c.expiresAt).toLocaleDateString()}`
                        ) : (
                          'Never'
                        )}
                      </small>
                    </div>
                  </td>
                  <td>
                    {(c.lastUsedAt || c.LastUsedAt) ? (
                      <span>{formatRelativeTime(c.lastUsedAt || c.LastUsedAt)}</span>
                    ) : (
                      <span className="badge badge-secondary">Never</span>
                    )}
                  </td>
                  <td>
                    <button
                      className="btn btn-secondary btn-sm"
                      data-testid="btn-rotate-client-secret"
                      onClick={() => rotateClientSecret(c.id)}
                      title="Rotate Secret"
                      style={{ marginRight: '6px' }}
                    >
                      <i className="fa-solid fa-arrows-rotate"></i> Rotate Secret
                    </button>
                    <button
                      className="btn btn-danger btn-sm"
                      data-testid="btn-delete-client"
                      onClick={() => deleteClient(c.id, c.displayName)}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {selectedClientForUris && (
        <RedirectUrisModal
          isOpen={true}
          onClose={() => setSelectedClientForUris(null)}
          clientName={selectedClientForUris.displayName}
          redirectUris={selectedClientForUris.redirectUris || []}
        />
      )}
    </div>
  );
};
