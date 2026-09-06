import React from 'react';
import { McpServer, useServerStore } from '../../stores/useServerStore';

interface ServerCardProps {
  server: McpServer;
}

export const ServerCard: React.FC<ServerCardProps> = ({ server }) => {
  const { toggleServerEnabled, reconnectServer, deleteServer, openEditModal, openInspectModal } = useServerStore();

  const isDisconnected = server.enabled && server.connectionStatus !== 'Connected';
  const itemClass = isDisconnected ? 'server-item server-disconnected-pulse' : 'server-item';
  const nameClass = server.enabled ? 'server-name' : 'server-name text-muted';
  const categoryBadge =
    server.categories && server.categories.length > 0
      ? server.categories.map((cat) => (
          <span key={cat} className="server-badge" style={{ background: 'rgba(59,130,246,0.1)', color: 'var(--primary)' }}>
            {cat}
          </span>
        ))
      : null;

  let statusBadge: React.ReactNode;
  let retryBtn: React.ReactNode = null;

  if (server.enabled) {
    const status = server.connectionStatus || 'Disconnected';
    if (status === 'Connected') {
      statusBadge = (
        <span className="server-badge badge-success">
          <span className="indicator online"></span> Connected
        </span>
      );
    } else if (status === 'Connecting' || status === 'Retrying') {
      const attemptText = server.connectionAttempts > 0 ? ` (${server.connectionAttempts}/5)` : '';
      statusBadge = (
        <span className="server-badge badge-warning">
          <i className="fa-solid fa-spinner fa-spin"></i> {status}
          {attemptText}
        </span>
      );
    } else if (status === 'Failed') {
      statusBadge = (
        <span className="server-badge badge-danger" title={server.connectionError || 'Connection failed'}>
          <i className="fa-solid fa-triangle-exclamation"></i> Failed
        </span>
      );
      retryBtn = (
        <button
          className="btn-icon btn-retry"
          title={`Retry Connection (Attempts: ${server.connectionAttempts})`}
          onClick={() => reconnectServer(server.id)}
          style={{ color: 'var(--accent)' }}
        >
          <i className="fa-solid fa-arrows-rotate"></i>
        </button>
      );
    } else {
      statusBadge = <span className="server-badge badge-secondary">Disconnected</span>;
      retryBtn = (
        <button
          className="btn-icon btn-retry"
          title="Connect Server"
          onClick={() => reconnectServer(server.id)}
          style={{ color: 'var(--primary)' }}
        >
          <i className="fa-solid fa-plug"></i>
        </button>
      );
    }
  } else {
    statusBadge = <span className="server-badge badge-secondary">Disabled</span>;
  }

  return (
    <div className={itemClass} data-server-id={server.id}>
      <div className="server-info">
        <div className="server-name-row">
          <span className={nameClass}>{server.displayName}</span>
          <span className="server-badge server-id-badge" title={`Server ID: ${server.id}`}>
            {server.id}
          </span>
          {server.alias && (
            <span
              className="server-badge badge-alias"
              title={`Alias: ${server.alias}`}
              style={{
                background: 'rgba(168, 85, 247, 0.15)',
                color: '#c084fc',
                border: '1px solid rgba(168, 85, 247, 0.3)',
              }}
            >
              <i className="fa-solid fa-tag" style={{ marginRight: '4px', fontSize: '0.75rem' }}></i>
              {server.alias}
            </span>
          )}
          <span className="server-badge">{(server.type || 'SSE').toUpperCase()}</span>
          {categoryBadge}
          {server.hasApiKey && (
            <span className="server-badge badge-key">
              <i className="fa-solid fa-lock"></i> Secured
            </span>
          )}
          {server.hidden && (
            <span className="server-badge">
              <i className="fa-solid fa-eye-slash"></i> Hidden
            </span>
          )}
          {statusBadge}
        </div>
        <span className="server-url">{server.url}</span>
      </div>
      <div className="server-actions">
        {retryBtn}
        <button
          className="btn-icon btn-inspect"
          title="Inspect Capabilities (Tools, Resources, Prompts)"
          onClick={() => openInspectModal(server)}
          style={{ color: 'var(--primary)' }}
        >
          <i className="fa-solid fa-magnifying-glass"></i>
        </button>
        <button
          className="btn-icon btn-edit"
          title="Edit Server Config"
          onClick={() => openEditModal(server)}
        >
          <i className="fa-solid fa-pen-to-square"></i>
        </button>
        <button
          className="btn-icon btn-delete"
          title="Delete Server"
          onClick={() => deleteServer(server.id, server.displayName)}
        >
          <i className="fa-solid fa-trash"></i>
        </button>
        <label className="switch" title={server.enabled ? 'Disable Server' : 'Enable Server'}>
          <input
            type="checkbox"
            checked={server.enabled}
            onChange={(e) => toggleServerEnabled(server.id, e.target.checked)}
          />
          <span className="slider"></span>
        </label>
      </div>
    </div>
  );
};
