import React from 'react';

interface ResourceItem {
  uri: string;
  name: string;
  mimeType?: string;
  description?: string;
}

interface TemplateItem {
  uriTemplate: string;
  name: string;
  description?: string;
}

interface ResourceTesterCardProps {
  resourcesData: { resources: ResourceItem[]; templates: TemplateItem[] };
  selectedServer: string;
  selectedResourceUri: string;
  selectedResourceValue: string;
  onServerChange: (srv: string) => void;
  onSelectChange: (val: string, type: string) => void;
  onUriChange: (val: string) => void;
  onSubmit: (e: React.FormEvent) => void;
}

export const ResourceTesterCard: React.FC<ResourceTesterCardProps> = ({
  resourcesData,
  selectedServer,
  selectedResourceUri,
  selectedResourceValue,
  onServerChange,
  onSelectChange,
  onUriChange,
  onSubmit,
}) => {
  const getResourceServers = () => {
    const servers = new Set<string>();
    servers.add('router');
    if (resourcesData.resources) {
      resourcesData.resources.forEach((r) => {
        if (r.uri) {
          const s = parseUriServer(r.uri);
          if (s) servers.add(s);
        }
      });
    }
    return Array.from(servers).sort();
  };

  const parseUriServer = (uri: string) => {
    if (uri.startsWith('router://') || uri.startsWith('logs://')) return 'router';
    try {
      const parsed = new URL(uri);
      if (parsed.protocol === 'mcp:') return parsed.hostname;
    } catch {
      // Ignore URL parsing exceptions for malformed or custom URIs
    }
    return null;
  };

  return (
    <div className="glass-card" data-testid="resource-tester-card">
      <h2>
        <i className="fa-solid fa-file-invoice"></i> Interactive Resource Tester
      </h2>
      <form onSubmit={onSubmit}>
        <div className="form-row">
          <div className="form-group">
            <label htmlFor="tester-resource-server">Server</label>
            <select
              id="tester-resource-server"
              data-testid="resource-server-select"
              value={selectedServer}
              onChange={(e) => onServerChange(e.target.value)}
              required
            >
              <option value="">-- Choose Server --</option>
              {getResourceServers().map((srv) => (
                <option key={srv} value={srv}>
                  {srv === 'router' ? 'Built-in Logs & Router state' : `${srv.toUpperCase()} Server`}
                </option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="tester-resource-name">Resource</label>
            <select
              id="tester-resource-name"
              data-testid="resource-name-select"
              value={selectedResourceValue}
              onChange={(e) => {
                const opt = e.target.selectedOptions[0];
                onSelectChange(e.target.value, opt?.dataset.type || '');
              }}
              required
            >
              <option value="">-- Choose Resource / Template --</option>
              {selectedServer === 'router' ? (
                <>
                  <option value="router://status" data-type="resource">
                    Router Status (router://status)
                  </option>
                  <option value="router://config" data-type="resource">
                    Router Config (router://config)
                  </option>
                  {resourcesData.templates?.map((t) => (
                    <option key={t.uriTemplate} value={t.uriTemplate} data-type="template">
                      [Template] {t.name} ({t.uriTemplate})
                    </option>
                  ))}
                </>
              ) : (
                resourcesData.resources
                  ?.filter((r) => parseUriServer(r.uri) === selectedServer)
                  .map((r) => (
                    <option key={r.uri} value={r.uri} data-type="resource">
                      {r.name} ({r.uri})
                    </option>
                  ))
              )}
            </select>
          </div>
        </div>

        <div className="form-group" style={{ marginTop: '15px' }}>
          <label htmlFor="tester-resource-uri">Resource URI</label>
          <input
            type="text"
            id="tester-resource-uri"
            data-testid="resource-uri-input"
            placeholder="e.g. mcp://plex/library/sections"
            value={selectedResourceUri}
            onChange={(e) => onUriChange(e.target.value)}
            required
          />
        </div>

        <div style={{ marginTop: '20px' }}>
          <button type="submit" className="btn btn-primary" data-testid="resource-read-btn" disabled={!selectedResourceUri}>
            <i className="fa-solid fa-play"></i> Read Resource
          </button>
        </div>
      </form>
    </div>
  );
};
