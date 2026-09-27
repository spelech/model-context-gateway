import React from 'react';
import { useServerStore } from '../../stores/useServerStore';

export const ServerInspectModal: React.FC = () => {
  const {
    isInspectOpen,
    inspectServer,
    inspectData,
    inspectLoading,
    inspectActiveTab,
    inspectSearchQuery,
    closeInspectModal,
    setInspectActiveTab,
    setInspectSearchQuery,
  } = useServerStore();

  if (!isInspectOpen || !inspectServer) return null;

  const tools = inspectData.tools || [];
  const resources = inspectData.resources || [];
  const prompts = inspectData.prompts || [];

  const filterItem = (item: any) => {
    if (!inspectSearchQuery) return true;
    const q = inspectSearchQuery.toLowerCase();
    const nameMatch = (item.name || '').toLowerCase().includes(q);
    const descMatch = (item.description || '').toLowerCase().includes(q);
    const uriMatch = (item.uri || '').toLowerCase().includes(q);
    return nameMatch || descMatch || uriMatch;
  };

  const filteredTools = tools.filter(filterItem);
  const filteredResources = resources.filter(filterItem);
  const filteredPrompts = prompts.filter(filterItem);

  return (
    <div id="inspect-modal" className="modal-backdrop" data-testid="server-inspect-modal" style={{ display: 'flex' }}>
      <div className="glass-card modal-card" style={{ maxWidth: '800px', width: '90%' }}>
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-microchip"></i> Capabilities: {inspectServer.displayName}
          </h2>
          <button className="btn-close" data-testid="server-inspect-close-btn" onClick={closeInspectModal}>
            &times;
          </button>
        </div>

        <div className="tester-tabs" style={{ marginBottom: '15px' }}>
          <button
            type="button"
            className={`tester-tab-btn ${inspectActiveTab === 'tools' ? 'active' : ''}`}
            data-testid="inspect-tab-tools"
            onClick={() => setInspectActiveTab('tools')}
          >
            <i className="fa-solid fa-wrench"></i> Tools ({tools.length})
          </button>
          <button
            type="button"
            className={`tester-tab-btn ${inspectActiveTab === 'resources' ? 'active' : ''}`}
            data-testid="inspect-tab-resources"
            onClick={() => setInspectActiveTab('resources')}
          >
            <i className="fa-solid fa-file-lines"></i> Resources ({resources.length})
          </button>
          <button
            type="button"
            className={`tester-tab-btn ${inspectActiveTab === 'prompts' ? 'active' : ''}`}
            data-testid="inspect-tab-prompts"
            onClick={() => setInspectActiveTab('prompts')}
          >
            <i className="fa-solid fa-comments"></i> Prompts ({prompts.length})
          </button>
        </div>

        <div className="inspect-search-box" style={{ marginBottom: '15px' }}>
          <input
            type="text"
            data-testid="inspect-search-input"
            placeholder={`Filter ${inspectActiveTab}...`}
            value={inspectSearchQuery}
            onChange={(e) => setInspectSearchQuery(e.target.value)}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', background: 'rgba(0,0,0,0.3)', border: '1px solid var(--border-color)', color: '#fff' }}
          />
        </div>

        <div className="inspect-content-container" style={{ maxHeight: '400px', overflowY: 'auto' }}>
          {inspectLoading ? (
            <div style={{ textAlign: 'center', padding: '30px', color: 'var(--text-muted)' }}>
              <i className="fa-solid fa-spinner fa-spin fa-2x"></i>
              <p style={{ marginTop: '10px' }}>Querying backend capabilities...</p>
            </div>
          ) : (
            <>
              {inspectActiveTab === 'tools' && (
                <div className="inspect-list">
                  {filteredTools.length === 0 ? (
                    <p style={{ color: 'var(--text-muted)', textAlign: 'center', padding: '20px' }}>No tools found.</p>
                  ) : (
                    filteredTools.map((tool, idx) => (
                      <div key={idx} className="inspect-item" style={{ background: 'rgba(255,255,255,0.03)', padding: '12px', borderRadius: '6px', marginBottom: '10px', border: '1px solid var(--border-color)' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <strong style={{ color: 'var(--accent)', fontFamily: 'monospace' }}>{tool.name}</strong>
                        </div>
                        {tool.description && <p style={{ fontSize: '12px', color: 'var(--text-muted)', margin: '5px 0' }}>{tool.description}</p>}
                        {tool.inputSchema && (
                          <details style={{ marginTop: '5px' }}>
                            <summary style={{ fontSize: '11px', color: 'var(--primary)', cursor: 'pointer' }}>Input Schema</summary>
                            <pre style={{ fontSize: '11px', background: 'rgba(0,0,0,0.4)', padding: '6px', borderRadius: '4px', marginTop: '4px', overflowX: 'auto' }}>
                              {JSON.stringify(tool.inputSchema, null, 2)}
                            </pre>
                          </details>
                        )}
                      </div>
                    ))
                  )}
                </div>
              )}

              {inspectActiveTab === 'resources' && (
                <div className="inspect-list">
                  {filteredResources.length === 0 ? (
                    <p style={{ color: 'var(--text-muted)', textAlign: 'center', padding: '20px' }}>No resources found.</p>
                  ) : (
                    filteredResources.map((res, idx) => (
                      <div key={idx} className="inspect-item" style={{ background: 'rgba(255,255,255,0.03)', padding: '12px', borderRadius: '6px', marginBottom: '10px', border: '1px solid var(--border-color)' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <strong style={{ color: 'var(--secondary)', fontFamily: 'monospace' }}>{res.name || res.uri}</strong>
                          {res.mimeType && <span className="server-badge" style={{ fontSize: '10px' }}>{res.mimeType}</span>}
                        </div>
                        <p style={{ fontSize: '11px', color: 'var(--text-muted)', margin: '4px 0', fontFamily: 'monospace' }}>{res.uri}</p>
                        {res.description && <p style={{ fontSize: '12px', color: 'var(--text-muted)', margin: '4px 0' }}>{res.description}</p>}
                      </div>
                    ))
                  )}
                </div>
              )}

              {inspectActiveTab === 'prompts' && (
                <div className="inspect-list">
                  {filteredPrompts.length === 0 ? (
                    <p style={{ color: 'var(--text-muted)', textAlign: 'center', padding: '20px' }}>No prompts found.</p>
                  ) : (
                    filteredPrompts.map((p, idx) => (
                      <div key={idx} className="inspect-item" style={{ background: 'rgba(255,255,255,0.03)', padding: '12px', borderRadius: '6px', marginBottom: '10px', border: '1px solid var(--border-color)' }}>
                        <strong style={{ color: 'var(--accent)', fontFamily: 'monospace' }}>{p.name}</strong>
                        {p.description && <p style={{ fontSize: '12px', color: 'var(--text-muted)', margin: '5px 0' }}>{p.description}</p>}
                        {p.arguments && p.arguments.length > 0 && (
                          <div style={{ marginTop: '6px' }}>
                            <span style={{ fontSize: '11px', color: 'var(--text-muted)' }}>Arguments:</span>
                            <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap', marginTop: '4px' }}>
                              {p.arguments.map((arg: any, aIdx: number) => (
                                <span key={aIdx} className="server-badge" style={{ fontSize: '10px' }}>
                                  {arg.name} {arg.required ? '*' : ''}
                                </span>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    ))
                  )}
                </div>
              )}
            </>
          )}
        </div>

        <div className="modal-footer" style={{ marginTop: '15px' }}>
          <button type="button" className="btn btn-secondary" data-testid="server-inspect-footer-close-btn" onClick={closeInspectModal}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
