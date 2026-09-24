import React, { useState } from 'react';
import { parseNamespacedName } from '../../shared/utils/mcpNaming';

interface ToolItem {
  name: string;
  description: string;
  inputSchema?: {
    type?: string;
    properties?: Record<string, any>;
    required?: string[];
  };
}

interface ToolTesterCardProps {
  tools: ToolItem[];
  selectedServer: string;
  selectedToolName: string;
  toolArguments: Record<string, any>;
  rawToolJson: string;
  onServerChange: (srv: string) => void;
  onToolChange: (name: string) => void;
  onArgChange: (key: string, type: string, val: any) => void;
  onBulkArgsChange?: (args: Record<string, any>) => void;
  onRawJsonChange: (val: string) => void;
  onSubmit: (e: React.FormEvent) => void;
}

export const ToolTesterCard: React.FC<ToolTesterCardProps> = ({
  tools,
  selectedServer,
  selectedToolName,
  toolArguments,
  rawToolJson,
  onServerChange,
  onToolChange,
  onArgChange,
  onBulkArgsChange,
  onRawJsonChange,
  onSubmit,
}) => {
  const [interactiveTab, setInteractiveTab] = useState<'form' | 'json'>('form');

  const getToolServers = () => {
    const servers = new Set<string>();
    let hasCustom = false;
    tools.forEach((t) => {
      const parsed = parseNamespacedName(t.name);
      if (parsed.isCustom) {
        hasCustom = true;
      } else {
        servers.add(parsed.serverId);
      }
    });
    if (hasCustom) {
      servers.add('custom');
    }
    return Array.from(servers).sort();
  };

  const getFilteredTools = () => {
    return tools
      .filter((t) => parseNamespacedName(t.name).serverId === selectedServer)
      .sort((a, b) => {
        const nameA = parseNamespacedName(a.name).cleanName;
        const nameB = parseNamespacedName(b.name).cleanName;
        return nameA.localeCompare(nameB);
      });
  };

  const currentTool = tools.find((t) => t.name === selectedToolName);
  const parsedCurrentTool = currentTool ? parseNamespacedName(currentTool.name) : null;

  const handlePrefillExample = () => {
    if (!currentTool?.inputSchema?.properties) return;
    const properties = currentTool.inputSchema.properties;
    const required = currentTool.inputSchema.required || [];
    const prefilled: Record<string, any> = {};

    for (const [key, prop] of Object.entries<any>(properties)) {
      if (prop.default !== undefined) {
        prefilled[key] = prop.default;
      } else if (prop.enum && Array.isArray(prop.enum) && prop.enum.length > 0) {
        prefilled[key] = prop.enum[0];
      } else if (prop.type === 'boolean') {
        prefilled[key] = false;
      } else if (prop.type === 'integer' || prop.type === 'number') {
        prefilled[key] = prop.examples?.[0] !== undefined ? prop.examples[0] : 0;
      } else if (prop.type === 'array') {
        prefilled[key] = prop.examples?.[0] !== undefined ? prop.examples[0] : [];
      } else if (prop.type === 'object') {
        prefilled[key] = prop.examples?.[0] !== undefined ? prop.examples[0] : {};
      } else {
        prefilled[key] = prop.examples?.[0] !== undefined
          ? prop.examples[0]
          : (required.includes(key) ? `sample_${key}` : 'example');
      }
    }

    if (onBulkArgsChange) {
      onBulkArgsChange(prefilled);
    } else {
      onRawJsonChange(JSON.stringify(prefilled, null, 2));
      for (const [key, prop] of Object.entries<any>(properties)) {
        onArgChange(key, prop.type || 'string', prefilled[key]);
      }
    }
  };

  const renderParameterSummary = () => {
    if (!currentTool) return null;
    const props = currentTool.inputSchema?.properties;
    if (!props || Object.keys(props).length === 0) {
      return 'Takes no arguments — ready to execute';
    }
    const reqCount = currentTool.inputSchema?.required?.length || 0;
    if (reqCount > 0) {
      return `${reqCount} required parameter${reqCount === 1 ? '' : 's'}`;
    }
    const totalCount = Object.keys(props).length;
    return `${totalCount} optional parameter${totalCount === 1 ? '' : 's'}`;
  };

  return (
    <div className="glass-card">
      <h2>
        <i className="fa-solid fa-wand-magic-sparkles"></i> Interactive Tool Tester
      </h2>
      <form onSubmit={onSubmit}>
        <div className="form-row">
          <div className="form-group">
            <label htmlFor="tester-server">Server</label>
            <select
              id="tester-server"
              value={selectedServer}
              onChange={(e) => onServerChange(e.target.value)}
              required
            >
              <option value="">-- Choose Server --</option>
              {getToolServers().map((srv) => (
                <option key={srv} value={srv}>
                  {srv === 'custom' ? 'Native C# Registry (custom)' : `${srv.toUpperCase()} Server`}
                </option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="tester-tool">Tool</label>
            <select
              id="tester-tool"
              value={selectedToolName}
              onChange={(e) => onToolChange(e.target.value)}
              required
            >
              <option value="">-- Choose Tool --</option>
              {getFilteredTools().map((t) => {
                const parsed = parseNamespacedName(t.name);
                return (
                  <option key={t.name} value={t.name}>
                    {parsed.cleanName}
                  </option>
                );
              })}
            </select>
          </div>
        </div>

        {currentTool && parsedCurrentTool && (
          <div className="tool-hint-banner">
            <div className="tool-hint-header">
              <div className="tool-hint-title-group">
                <span className="tool-hint-name">{parsedCurrentTool.cleanName}</span>
                <span className="badge badge-primary">[{parsedCurrentTool.serverId.toUpperCase()}]</span>
              </div>
              <span className="badge">{renderParameterSummary()}</span>
            </div>
            {currentTool.description && (
              <p className="tool-hint-desc">{currentTool.description}</p>
            )}
          </div>
        )}

        <div className="tester-tabs">
          <button
            type="button"
            className={`tester-tab-btn ${interactiveTab === 'form' ? 'active' : ''}`}
            onClick={() => setInteractiveTab('form')}
          >
            Interactive Form
          </button>
          <button
            type="button"
            className={`tester-tab-btn ${interactiveTab === 'json' ? 'active' : ''}`}
            onClick={() => setInteractiveTab('json')}
          >
            Raw JSON Input
          </button>
          {currentTool && (
            <button
              type="button"
              className="btn btn-prefill"
              style={{ marginLeft: 'auto' }}
              onClick={handlePrefillExample}
              title="Pre-fill with example parameters"
            >
              <i className="fa-solid fa-wand-magic-sparkles"></i> Pre-fill Example
            </button>
          )}
        </div>

        {interactiveTab === 'form' ? (
          <div className="tester-tab-content active">
            <div id="dynamic-form-fields">
              {selectedToolName && currentTool ? (
                renderDynamicFields(currentTool, toolArguments, onArgChange)
              ) : (
                <div className="empty-state">Select a tool to generate parameters.</div>
              )}
            </div>
          </div>
        ) : (
          <div className="tester-tab-content active">
            <div className="form-group">
              <label htmlFor="tester-raw-json">Arguments (JSON)</label>
              <textarea
                id="tester-raw-json"
                rows={8}
                placeholder="{}"
                value={rawToolJson}
                onChange={(e) => onRawJsonChange(e.target.value)}
              ></textarea>
            </div>
          </div>
        )}

        <div style={{ marginTop: '20px' }}>
          <button type="submit" className="btn btn-primary" disabled={!selectedToolName}>
            <i className="fa-solid fa-play"></i> Run Tool
          </button>
        </div>
      </form>
    </div>
  );
};

const renderDynamicFields = (
  tool: ToolItem,
  args: Record<string, any>,
  onChange: (key: string, type: string, val: any) => void
) => {
  if (!tool.inputSchema || !tool.inputSchema.properties) {
    return <div className="empty-state">This tool takes no arguments.</div>;
  }

  const properties = tool.inputSchema.properties;
  const required = tool.inputSchema.required || [];

  return Object.entries(properties).map(([key, prop]: [string, any]) => {
    const isRequired = required.includes(key);
    const reqText = isRequired ? <span style={{ color: 'var(--status-offline)' }}>*</span> : null;

    if (prop.enum && Array.isArray(prop.enum)) {
      return (
        <div key={key} className="param-field">
          <label htmlFor={`param-${key}`}>
            {key} {reqText} <span className="type-badge">enum</span>
          </label>
          <select
            id={`param-${key}`}
            value={args[key] !== undefined ? args[key] : (prop.default !== undefined ? prop.default : '')}
            onChange={(e) => onChange(key, prop.type || 'string', e.target.value)}
            required={isRequired}
          >
            <option value="">-- Select {key} --</option>
            {prop.enum.map((opt: any) => (
              <option key={String(opt)} value={String(opt)}>
                {String(opt)}
              </option>
            ))}
          </select>
          {prop.description && <div className="field-desc">{prop.description}</div>}
        </div>
      );
    }

    if (prop.type === 'boolean') {
      return (
        <div key={key} className="param-field checkbox-field">
          <label className="switch" htmlFor={`param-${key}`}>
            <input
              id={`param-${key}`}
              type="checkbox"
              checked={!!args[key]}
              onChange={(e) => onChange(key, 'boolean', e.target.checked)}
            />
            <span className="slider"></span>
          </label>
          <label htmlFor={`param-${key}`}>
            {key} {reqText} <span className="type-badge">boolean</span>
          </label>
          {prop.description && <div className="field-desc">{prop.description}</div>}
        </div>
      );
    }

    if (prop.type === 'integer' || prop.type === 'number') {
      const placeholder = prop.default !== undefined
        ? String(prop.default)
        : (prop.examples?.[0] !== undefined ? String(prop.examples[0]) : '');
      return (
        <div key={key} className="param-field">
          <label htmlFor={`param-${key}`}>
            {key} {reqText} <span className="type-badge">{prop.type || 'number'}</span>
          </label>
          <input
            id={`param-${key}`}
            type="number"
            step={prop.type === 'integer' ? '1' : 'any'}
            placeholder={placeholder}
            value={args[key] !== undefined ? args[key] : ''}
            onChange={(e) => onChange(key, 'number', e.target.value)}
            required={isRequired}
          />
          {prop.description && <div className="field-desc">{prop.description}</div>}
        </div>
      );
    }

    if (prop.type === 'array' || prop.type === 'object') {
      const displayVal = typeof args[key] === 'object' ? JSON.stringify(args[key], null, 2) : (args[key] || '');
      const placeholder = prop.default !== undefined
        ? (typeof prop.default === 'object' ? JSON.stringify(prop.default) : String(prop.default))
        : (prop.examples?.[0] !== undefined
            ? (typeof prop.examples[0] === 'object' ? JSON.stringify(prop.examples[0]) : String(prop.examples[0]))
            : (prop.type === 'array' ? '["item1", "item2"]' : '{"key": "value"}'));
      return (
        <div key={key} className="param-field">
          <label htmlFor={`param-${key}`}>
            {key} {reqText} <span className="type-badge">{prop.type}</span>
          </label>
          <textarea
            id={`param-${key}`}
            rows={2}
            placeholder={placeholder}
            value={displayVal}
            onChange={(e) => onChange(key, prop.type, e.target.value)}
            required={isRequired}
          />
          {prop.description && <div className="field-desc">{prop.description}</div>}
        </div>
      );
    }

    const placeholder = prop.default !== undefined
      ? String(prop.default)
      : (prop.examples?.[0] !== undefined ? String(prop.examples[0]) : '');
    return (
      <div key={key} className="param-field">
        <label htmlFor={`param-${key}`}>
          {key} {reqText} <span className="type-badge">{prop.type || 'string'}</span>
        </label>
        <input
          id={`param-${key}`}
          type="text"
          placeholder={placeholder}
          value={args[key] !== undefined ? args[key] : ''}
          onChange={(e) => onChange(key, 'string', e.target.value)}
          required={isRequired}
        />
        {prop.description && <div className="field-desc">{prop.description}</div>}
      </div>
    );
  });
};
