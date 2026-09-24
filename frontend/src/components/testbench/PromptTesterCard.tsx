import React from 'react';
import { parseNamespacedName } from '../../shared/utils/mcpNaming';

interface PromptItem {
  name: string;
  description: string;
  arguments?: {
    name: string;
    description?: string;
    required?: boolean;
  }[];
}

interface PromptTesterCardProps {
  prompts: PromptItem[];
  selectedServer: string;
  selectedPromptName: string;
  promptArguments: Record<string, string>;
  onServerChange: (srv: string) => void;
  onPromptChange: (name: string) => void;
  onArgChange: (name: string, val: string) => void;
  onSubmit: (e: React.FormEvent) => void;
}

export const PromptTesterCard: React.FC<PromptTesterCardProps> = ({
  prompts,
  selectedServer,
  selectedPromptName,
  promptArguments,
  onServerChange,
  onPromptChange,
  onArgChange,
  onSubmit,
}) => {
  const getPromptServers = () => {
    const servers = new Set<string>();
    let hasRouter = false;
    prompts.forEach((p) => {
      const parsed = parseNamespacedName(p.name, 'router');
      if (parsed.serverId === 'router' || parsed.isCustom) {
        hasRouter = true;
      } else {
        servers.add(parsed.serverId);
      }
    });
    if (hasRouter) {
      servers.add('router');
    }
    return Array.from(servers).sort();
  };

  const getFilteredPrompts = () => {
    return prompts
      .filter((p) => parseNamespacedName(p.name, 'router').serverId === selectedServer)
      .sort((a, b) => {
        const nameA = parseNamespacedName(a.name, 'router').cleanName;
        const nameB = parseNamespacedName(b.name, 'router').cleanName;
        return nameA.localeCompare(nameB);
      });
  };

  const currentPrompt = prompts.find((p) => p.name === selectedPromptName);
  const parsedCurrentPrompt = currentPrompt ? parseNamespacedName(currentPrompt.name, 'router') : null;

  const renderArgumentSummary = () => {
    if (!currentPrompt) return null;
    const args = currentPrompt.arguments;
    if (!args || args.length === 0) {
      return 'Takes no arguments — ready to execute';
    }
    const reqCount = args.filter((a) => a.required).length;
    if (reqCount > 0) {
      return `${reqCount} required argument${reqCount === 1 ? '' : 's'}`;
    }
    return `${args.length} optional argument${args.length === 1 ? '' : 's'}`;
  };

  return (
    <div className="glass-card">
      <h2>
        <i className="fa-solid fa-comments"></i> Interactive Prompt Tester
      </h2>
      <form onSubmit={onSubmit}>
        <div className="form-row">
          <div className="form-group">
            <label htmlFor="tester-prompt-server">Server</label>
            <select
              id="tester-prompt-server"
              value={selectedServer}
              onChange={(e) => onServerChange(e.target.value)}
              required
            >
              <option value="">-- Choose Server --</option>
              {getPromptServers().map((srv) => (
                <option key={srv} value={srv}>
                  {srv === 'router' ? 'Built-in Meta Workflows (router)' : `${srv.toUpperCase()} Server`}
                </option>
              ))}
            </select>
          </div>
          <div className="form-group">
            <label htmlFor="tester-prompt-name">Prompt</label>
            <select
              id="tester-prompt-name"
              value={selectedPromptName}
              onChange={(e) => onPromptChange(e.target.value)}
              required
            >
              <option value="">-- Choose Prompt --</option>
              {getFilteredPrompts().map((p) => {
                const parsed = parseNamespacedName(p.name, 'router');
                return (
                  <option key={p.name} value={p.name}>
                    {parsed.cleanName}
                  </option>
                );
              })}
            </select>
          </div>
        </div>

        {currentPrompt && parsedCurrentPrompt && (
          <div className="tool-hint-banner">
            <div className="tool-hint-header">
              <div className="tool-hint-title-group">
                <span className="tool-hint-name">{parsedCurrentPrompt.cleanName}</span>
                <span className="badge badge-primary">[{parsedCurrentPrompt.serverId.toUpperCase()}]</span>
              </div>
              <span className="badge">{renderArgumentSummary()}</span>
            </div>
            {currentPrompt.description && (
              <p className="tool-hint-desc">{currentPrompt.description}</p>
            )}
          </div>
        )}

        <div id="prompt-dynamic-fields" style={{ marginTop: '15px' }}>
          {selectedPromptName && currentPrompt ? (
            renderPromptFields(currentPrompt, promptArguments, onArgChange)
          ) : (
            <div className="empty-state">Select a prompt to generate arguments.</div>
          )}
        </div>

        <div style={{ marginTop: '20px' }}>
          <button type="submit" className="btn btn-primary" disabled={!selectedPromptName}>
            <i className="fa-solid fa-play"></i> Get Prompt Messages
          </button>
        </div>
      </form>
    </div>
  );
};

const renderPromptFields = (
  prompt: PromptItem,
  args: Record<string, string>,
  onChange: (name: string, val: string) => void
) => {
  if (!prompt.arguments || prompt.arguments.length === 0) {
    return <div className="empty-state">This prompt takes no arguments.</div>;
  }

  return prompt.arguments.map((arg) => {
    const reqText = arg.required ? <span style={{ color: 'var(--status-offline)' }}>*</span> : null;
    return (
      <div key={arg.name} className="param-field">
        <label htmlFor={`prompt-param-${arg.name}`}>
          {arg.name} {reqText} <span className="type-badge">string</span>
        </label>
        <input
          id={`prompt-param-${arg.name}`}
          type="text"
          placeholder={arg.description || `Enter ${arg.name}...`}
          value={args[arg.name] || ''}
          onChange={(e) => onChange(arg.name, e.target.value)}
          required={arg.required}
        />
        {arg.description && <div className="field-desc">{arg.description}</div>}
      </div>
    );
  });
};
