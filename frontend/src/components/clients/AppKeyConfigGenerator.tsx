import React, { useState } from 'react';
import { showToast } from '../../stores/useToastStore';

export interface AppKeyConfigGeneratorProps {
  plaintextKey: string;
}

type ConfigTab = 'claude' | 'cursor' | 'windsurf' | 'generic';

export const AppKeyConfigGenerator: React.FC<AppKeyConfigGeneratorProps> = ({ plaintextKey }) => {
  const [activeTab, setActiveTab] = useState<ConfigTab>('claude');
  const [copiedConfig, setCopiedConfig] = useState(false);

  const getBaseUrl = (): string => {
    if (typeof window !== 'undefined' && window.location && window.location.origin && window.location.origin !== 'null') {
      return window.location.origin;
    }
    return 'http://10.0.0.10:8026';
  };

  const origin = getBaseUrl().replace(/\/+$/, '');
  const endpointUrl = `${origin}/sse`;

  const tabs: { id: ConfigTab; label: string; fileHint: string }[] = [
    { id: 'claude', label: 'Claude Desktop', fileHint: 'claude_desktop_config.json' },
    { id: 'cursor', label: 'Cursor', fileHint: '.cursor/mcp.json' },
    { id: 'windsurf', label: 'Windsurf', fileHint: '~/.codeium/windsurf/mcp_config.json' },
    { id: 'generic', label: 'Generic JSON', fileHint: 'mcp_config.json' },
  ];

  const getConfigSnippet = (tab: ConfigTab): string => {
    switch (tab) {
      case 'claude':
        return JSON.stringify(
          {
            mcpServers: {
              'model-context-gateway': {
                url: endpointUrl,
                headers: {
                  'X-App-Key': plaintextKey,
                },
              },
            },
          },
          null,
          2
        );
      case 'cursor':
        return JSON.stringify(
          {
            mcpServers: {
              'model-context-gateway': {
                url: endpointUrl,
                type: 'sse',
                headers: {
                  'X-App-Key': plaintextKey,
                },
              },
            },
          },
          null,
          2
        );
      case 'windsurf':
        return JSON.stringify(
          {
            mcpServers: {
              'model-context-gateway': {
                serverUrl: endpointUrl,
                headers: {
                  'X-App-Key': plaintextKey,
                },
              },
            },
          },
          null,
          2
        );
      case 'generic':
      default:
        return JSON.stringify(
          {
            mcpServers: {
              'model-context-gateway': {
                url: endpointUrl,
                type: 'sse',
                headers: {
                  'X-App-Key': plaintextKey,
                },
              },
            },
          },
          null,
          2
        );
    }
  };

  const currentSnippet = getConfigSnippet(activeTab);

  const handleCopyConfig = async () => {
    try {
      if (navigator?.clipboard?.writeText) {
        await navigator.clipboard.writeText(currentSnippet);
      }
      setCopiedConfig(true);
      showToast('Configuration copied to clipboard!', 'success');
      setTimeout(() => setCopiedConfig(false), 2000);
    } catch {
      showToast('Failed to copy configuration', 'error');
    }
  };

  return (
    <div className="config-generator" style={{ marginTop: '14px' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '6px' }}>
        <h5 style={{ margin: 0, fontSize: '12px', color: 'var(--secondary)' }}>
          Ready-to-Use mcp_config.json Snippet:
        </h5>
        <span style={{ fontSize: '11px', color: '#94a3b8' }}>
          {tabs.find((t) => t.id === activeTab)?.fileHint}
        </span>
      </div>

      <div style={{ display: 'flex', gap: '6px', marginBottom: '8px', flexWrap: 'wrap' }}>
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={`btn btn-sm ${activeTab === tab.id ? 'btn-primary' : 'btn-secondary'}`}
            onClick={() => setActiveTab(tab.id)}
            style={{
              fontSize: '11px',
              padding: '4px 10px',
              borderRadius: '4px',
              transition: 'all 0.15s ease',
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <pre
        style={{
          background: '#090d16',
          padding: '10px 12px',
          borderRadius: '6px',
          fontSize: '11px',
          maxHeight: '150px',
          overflowY: 'auto',
          color: '#cbd5e1',
          border: '1px solid rgba(255,255,255,0.08)',
          margin: 0,
        }}
      >
        <code>{currentSnippet}</code>
      </pre>

      <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '6px' }}>
        <button
          type="button"
          className="btn btn-secondary btn-sm"
          onClick={handleCopyConfig}
          style={{ fontSize: '11px', padding: '4px 10px' }}
        >
          <i className={`fa-solid ${copiedConfig ? 'fa-check' : 'fa-copy'}`}></i>{' '}
          {copiedConfig ? 'Copied Configuration!' : 'Copy Configuration'}
        </button>
      </div>
    </div>
  );
};

export default AppKeyConfigGenerator;
