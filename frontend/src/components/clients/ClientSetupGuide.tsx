import React, { useEffect, useState } from 'react';
import { fetchServersApi } from '../../api/serverApi';
import { fetchAppKeysApi } from '../../api/appKeyApi';
import { showToast } from '../../stores/useToastStore';
import { McpServer, AppKeyItem } from '../../shared/types';

export const ClientSetupGuide: React.FC = () => {
  const [selectedFormat, setSelectedFormat] = useState<'standard' | 'vscode' | 'generic'>('standard');
  const [domainOption, setDomainOption] = useState<'origin' | 'lan' | 'custom'>('origin');
  const [customDomain, setCustomDomain] = useState<string>('https://mcp.wileyriley.com');
  const [serverScope, setServerScope] = useState<string>('all');
  const [metaMode, setMetaMode] = useState<boolean>(true);
  const [selectedKey, setSelectedKey] = useState<string>('');
  const [servers, setServers] = useState<McpServer[]>([]);
  const [appKeys, setAppKeys] = useState<AppKeyItem[]>([]);

  useEffect(() => {
    let isMounted = true;
    Promise.all([
      fetchServersApi().catch(() => []),
      fetchAppKeysApi().catch(() => [])
    ]).then(([fetchedServers, fetchedKeys]) => {
      if (isMounted) {
        setServers(fetchedServers || []);
        setAppKeys(fetchedKeys || []);
      }
    });
    return () => {
      isMounted = false;
    };
  }, []);

  const getBaseUrl = (): string => {
    if (domainOption === 'lan') {
      return 'http://10.0.0.10:8026';
    }
    if (domainOption === 'custom') {
      return customDomain.trim() || 'http://10.0.0.10:8026';
    }
    if (typeof window !== 'undefined' && window.location && window.location.origin && window.location.origin !== 'null') {
      return window.location.origin;
    }
    return 'http://10.0.0.10:8026';
  };

  const baseUrl = getBaseUrl().replace(/\/+$/, '');

  const getEndpointUrl = (): string => {
    if (serverScope === 'all') {
      return `${baseUrl}/sse?meta=${metaMode ? 'true' : 'false'}`;
    }
    return `${baseUrl}/${serverScope}`;
  };

  const endpointUrl = getEndpointUrl();
  const effectiveKey = selectedKey || 'mcp_live_YOUR_APP_KEY_HERE';

  const getConfigObject = () => {
    switch (selectedFormat) {
      case 'vscode':
        return {
          "mcp.servers": {
            "model-context-gateway": {
              "type": "sse",
              "url": endpointUrl,
              "headers": {
                "X-App-Key": effectiveKey
              }
            }
          }
        };
      case 'generic':
        return {
          "sseEndpoint": endpointUrl,
          "messageEndpoint": `${baseUrl}/message?sessionId={sessionId}`,
          "authHeader": `X-App-Key: ${effectiveKey}`
        };
      case 'standard':
      default:
        return {
          "mcpServers": {
            "model-context-gateway": {
              "url": endpointUrl,
              "headers": {
                "X-App-Key": effectiveKey
              }
            }
          }
        };
    }
  };

  const configJson = JSON.stringify(getConfigObject(), null, 2);

  const handleCopy = async () => {
    try {
      if (navigator?.clipboard?.writeText) {
        await navigator.clipboard.writeText(configJson);
      }
      showToast('Configuration copied to clipboard!', 'success');
    } catch {
      showToast('Failed to copy configuration to clipboard', 'error');
    }
  };

  return (
    <div className="glass-card guide-card">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px', marginBottom: '12px' }}>
        <div>
          <h2 style={{ margin: 0 }}>
            <i className="fa-solid fa-book"></i> Client Connection Guide
          </h2>
          <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: '4px 0 0 0' }}>
            Connect your preferred AI client or IDE extension to the unified Model Context Gateway.
          </p>
        </div>
      </div>

      <div className="tester-tabs" style={{ marginBottom: '16px', display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
        <button
          type="button"
          className={`tester-tab-btn ${selectedFormat === 'standard' ? 'active' : ''}`}
          onClick={() => setSelectedFormat('standard')}
        >
          <i className="fa-solid fa-code"></i> Standard (Claude / Cursor / AGY)
        </button>
        <button
          type="button"
          className={`tester-tab-btn ${selectedFormat === 'vscode' ? 'active' : ''}`}
          onClick={() => setSelectedFormat('vscode')}
        >
          <i className="fa-brands fa-windows"></i> VS Code
        </button>
        <button
          type="button"
          className={`tester-tab-btn ${selectedFormat === 'generic' ? 'active' : ''}`}
          onClick={() => setSelectedFormat('generic')}
        >
          <i className="fa-solid fa-network-wired"></i> Generic SSE
        </button>
      </div>

      <div
        className="guide-controls-panel"
        style={{
          background: 'rgba(255, 255, 255, 0.03)',
          border: '1px solid var(--border-color)',
          borderRadius: '8px',
          padding: '14px',
          marginBottom: '16px',
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: '14px',
          alignItems: 'end'
        }}
      >
        {/* Domain Selector */}
        <div>
          <label style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block', marginBottom: '6px', fontWeight: 600 }}>
            <i className="fa-solid fa-globe"></i> Domain / Host
          </label>
          <div style={{ display: 'flex', gap: '4px', marginBottom: domainOption === 'custom' ? '6px' : '0' }}>
            <button
              type="button"
              className={`btn btn-sm ${domainOption === 'origin' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ flex: 1, padding: '4px 8px', fontSize: '12px' }}
              onClick={() => setDomainOption('origin')}
            >
              Current Host
            </button>
            <button
              type="button"
              className={`btn btn-sm ${domainOption === 'lan' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ flex: 1, padding: '4px 8px', fontSize: '12px' }}
              onClick={() => setDomainOption('lan')}
            >
              Local LAN
            </button>
            <button
              type="button"
              className={`btn btn-sm ${domainOption === 'custom' ? 'btn-primary' : 'btn-secondary'}`}
              style={{ flex: 1, padding: '4px 8px', fontSize: '12px' }}
              onClick={() => setDomainOption('custom')}
            >
              Custom
            </button>
          </div>
          {domainOption === 'custom' && (
            <input
              type="text"
              value={customDomain}
              onChange={(e) => setCustomDomain(e.target.value)}
              placeholder="https://example.com"
              style={{
                width: '100%',
                padding: '6px 10px',
                borderRadius: '6px',
                background: 'rgba(0,0,0,0.3)',
                color: '#fff',
                border: '1px solid var(--glass-border)',
                fontSize: '12px'
              }}
            />
          )}
        </div>

        {/* Server Scope */}
        <div>
          <label htmlFor="server-scope-select" style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block', marginBottom: '6px', fontWeight: 600 }}>
            <i className="fa-solid fa-layer-group"></i> Server Scope
          </label>
          <select
            id="server-scope-select"
            data-testid="server-scope-select"
            value={serverScope}
            onChange={(e) => setServerScope(e.target.value)}
            style={{
              width: '100%',
              padding: '6px 10px',
              borderRadius: '6px',
              background: 'rgba(0,0,0,0.3)',
              color: '#fff',
              border: '1px solid var(--glass-border)',
              fontSize: '12px'
            }}
          >
            <option value="all">All Servers (Unified Gateway)</option>
            {servers.map((s) => (
              <option key={s.id} value={s.id}>
                {s.displayName || s.id} ({s.id})
              </option>
            ))}
          </select>
        </div>

        {/* Meta-Mode Toggle */}
        {serverScope === 'all' && (
          <div>
            <label style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block', marginBottom: '6px', fontWeight: 600 }}>
              <i className="fa-solid fa-wand-magic-sparkles"></i> Routing Mode
            </label>
            <label
              htmlFor="meta-mode-toggle"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '10px',
                cursor: 'pointer',
                padding: '8px 14px',
                background: 'rgba(0,0,0,0.2)',
                borderRadius: '6px',
                border: '1px solid var(--glass-border)',
                fontSize: '13px',
                minHeight: '40px'
              }}
            >
              <input
                type="checkbox"
                id="meta-mode-toggle"
                aria-label="Toggle Meta-Mode Dynamic Routing"
                checked={metaMode}
                onChange={(e) => setMetaMode(e.target.checked)}
                style={{ cursor: 'pointer', width: '24px', height: '24px', minWidth: '24px', minHeight: '24px', accentColor: 'var(--primary)' }}
              />
              <span style={{ fontWeight: 500, minHeight: '24px', display: 'inline-flex', alignItems: 'center' }}>Meta-Mode (Dynamic Routing)</span>
            </label>
          </div>
        )}

        {/* App Key */}
        <div>
          <label htmlFor="app-key-select" style={{ fontSize: '12px', color: 'var(--text-muted)', display: 'block', marginBottom: '6px', fontWeight: 600 }}>
            <i className="fa-solid fa-key"></i> App Key Credential
          </label>
          <select
            id="app-key-select"
            data-testid="app-key-select"
            value={selectedKey}
            onChange={(e) => setSelectedKey(e.target.value)}
            style={{
              width: '100%',
              padding: '6px 10px',
              borderRadius: '6px',
              background: 'rgba(0,0,0,0.3)',
              color: '#fff',
              border: '1px solid var(--glass-border)',
              fontSize: '12px'
            }}
          >
            <option value="">Default Placeholder (mcp_live_...)</option>
            {appKeys.map((k) => (
              <option key={k.id} value={k.keyPrefix ? `${k.keyPrefix}...` : k.id}>
                {k.name} ({k.keyPrefix ? `${k.keyPrefix}...` : k.id})
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="guide-content" style={{ background: 'rgba(0,0,0,0.25)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border-color)', fontSize: '13px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px', marginBottom: '10px' }}>
          <span style={{ color: 'var(--text-muted)', fontSize: '12px', flex: '1', minWidth: '200px' }}>
            {selectedFormat === 'standard' && 'Add to your client configuration file (claude_desktop_config.json / agy settings / Cursor / Cline):'}
            {selectedFormat === 'vscode' && 'Add to your VS Code MCP configuration (mcp.json):'}
            {selectedFormat === 'generic' && 'Direct connection endpoints for custom SSE MCP clients:'}
          </span>
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={handleCopy}
            style={{ whiteSpace: 'nowrap', flexShrink: 0 }}
          >
            <i className="fa-solid fa-copy"></i> Copy Configuration
          </button>
        </div>

        <pre
          style={{
            margin: 0,
            padding: '12px',
            background: 'rgba(0,0,0,0.4)',
            borderRadius: '6px',
            border: '1px solid rgba(255,255,255,0.05)',
            fontFamily: 'JetBrains Mono, monospace',
            fontSize: '12px',
            color: 'var(--accent, #38bdf8)',
            overflowX: 'auto',
            whiteSpace: 'pre'
          }}
        >
          {configJson}
        </pre>
      </div>
    </div>
  );
};
