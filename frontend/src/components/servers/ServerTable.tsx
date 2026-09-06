import React from 'react';
import { McpServer } from '../../shared/types';

interface ServerTableProps {
  servers: McpServer[];
}

export const ServerTable: React.FC<ServerTableProps> = ({ servers }) => {
  return (
    <div className="table-container">
      <table className="data-table server-table">
        <thead>
          <tr>
            <th>Server ID</th>
            <th>Alias</th>
            <th>Display Name</th>
            <th>Type</th>
            <th>URL</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {servers.length === 0 && (
            <tr>
              <td colSpan={6} style={{ textAlign: 'center', color: 'var(--text-muted)' }}>
                No MCP servers configured.
              </td>
            </tr>
          )}
          {servers.map((server) => (
            <tr key={server.id} data-server-id={server.id}>
              <td>
                <span className="server-id-label">{server.id}</span>
                {server.alias && (
                  <span
                    className="server-badge badge-alias"
                    style={{
                      marginLeft: '8px',
                      background: 'rgba(168, 85, 247, 0.15)',
                      color: '#c084fc',
                      border: '1px solid rgba(168, 85, 247, 0.3)',
                    }}
                  >
                    <i className="fa-solid fa-tag" style={{ marginRight: '4px', fontSize: '0.75rem' }}></i>
                    {server.alias}
                  </span>
                )}
              </td>
              <td>
                {server.alias ? (
                  <span
                    className="server-badge badge-alias"
                    style={{
                      background: 'rgba(168, 85, 247, 0.15)',
                      color: '#c084fc',
                      border: '1px solid rgba(168, 85, 247, 0.3)',
                    }}
                  >
                    {server.alias}
                  </span>
                ) : (
                  <span className="text-muted" style={{ color: 'var(--text-muted)' }}>—</span>
                )}
              </td>
              <td>{server.displayName}</td>
              <td>
                <span className="server-badge">{(server.type || 'SSE').toUpperCase()}</span>
              </td>
              <td className="server-url">{server.url}</td>
              <td>
                <span className={`server-badge ${server.connectionStatus === 'Connected' ? 'badge-success' : 'badge-secondary'}`}>
                  {server.connectionStatus || (server.enabled ? 'Connected' : 'Disabled')}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
