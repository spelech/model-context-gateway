import React, { useState } from 'react';
import { EmbeddingSettings } from '../../shared/types';
import { showToast } from '../../stores/useToastStore';
import { useSettingsStore } from '../../stores/useSettingsStore';

export interface CodeModeTabProps {
  settings: Partial<EmbeddingSettings> | null;
  availableGroups?: string[];
  onSave?: (settings: EmbeddingSettings) => Promise<boolean | void> | boolean | void;
}

export const CodeModeTab: React.FC<CodeModeTabProps> = ({ settings, availableGroups = [], onSave }) => {
  const [enableCodeMode, setEnableCodeMode] = useState<boolean>(settings?.enableCodeMode ?? true);
  const [timeoutSeconds, setTimeoutSeconds] = useState<number>(settings?.codeModeExecutionTimeoutSeconds ?? 30);
  const [maxToolCalls, setMaxToolCalls] = useState<number>(settings?.codeModeMaxToolCalls ?? 50);
  const [maxMemoryMb, setMaxMemoryMb] = useState<number>(settings?.codeModeMaxMemoryMb ?? 64);
  const [maxStatements, setMaxStatements] = useState<number>(settings?.codeModeMaxStatements ?? 100000);

  const [allowedGroups, setAllowedGroups] = useState<string[]>(() => {
    return (settings?.codeModeAllowedGroups || '')
      .split(',')
      .map((g) => g.trim())
      .filter(Boolean);
  });

  const [newGroupInput, setNewGroupInput] = useState<string>('');
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saved' | 'error'>('idle');

  const handleAddGroup = (groupToAdd?: string) => {
    const candidate = (groupToAdd !== undefined ? groupToAdd : newGroupInput).trim();
    if (!candidate) return;
    if (!allowedGroups.includes(candidate)) {
      setAllowedGroups([...allowedGroups, candidate]);
    }
    setNewGroupInput('');
  };

  const handleRemoveGroup = (groupToRemove: string) => {
    setAllowedGroups(allowedGroups.filter((g) => g !== groupToRemove));
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      handleAddGroup();
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);
    setSaveStatus('idle');

    const updatedSettings: EmbeddingSettings = {
      embeddingProvider: 'local',
      embeddingModelDir: 'data/models',
      embeddingApiUrl: '',
      embeddingApiModel: 'all-MiniLM-L6-v2',
      embeddingApiKey: '',
      ...(settings ?? {}),
      enableCodeMode,
      codeModeExecutionTimeoutSeconds: Number(timeoutSeconds),
      codeModeMaxToolCalls: Number(maxToolCalls),
      codeModeMaxMemoryMb: Number(maxMemoryMb),
      codeModeMaxStatements: Number(maxStatements),
      codeModeAllowedGroups: allowedGroups.join(','),
    };

    try {
      if (onSave) {
        await onSave(updatedSettings);
      } else {
        await useSettingsStore.getState().saveEmbeddingSettings(updatedSettings);
      }
      setSaveStatus('saved');
      showToast('Code Mode settings saved successfully', 'success');
      setTimeout(() => setSaveStatus('idle'), 2500);
    } catch (err: any) {
      setSaveStatus('error');
      showToast(err.message || 'Failed to save Code Mode settings', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const unselectedKnownGroups = availableGroups.filter(
    (g) => g && !allowedGroups.includes(g)
  );

  return (
    <div id="subview-codemode" data-testid="code-mode-tab" className="settings-subview active">
      <div className="glass-card settings-card" style={{ maxWidth: '750px', margin: '0 auto' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '15px' }}>
          <div>
            <h2 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: '10px' }}>
              <i className="fa-solid fa-code" style={{ color: 'var(--primary)' }}></i> Code Mode
            </h2>
            <p className="description" style={{ color: 'var(--text-muted)', fontSize: '13px', marginTop: '6px', marginBottom: 0 }}>
              Allow LLMs to run Sandboxed JavaScript orchestration scripts directly against MCP backend tools.
            </p>
          </div>
          <span
            className={`badge ${enableCodeMode ? 'badge-success' : 'badge-secondary'}`}
            style={{
              padding: '6px 12px',
              borderRadius: '20px',
              fontSize: '12px',
              fontWeight: 600,
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            <i className={`fa-solid ${enableCodeMode ? 'fa-circle-check' : 'fa-circle-xmark'}`}></i>
            {enableCodeMode ? 'Active' : 'Disabled'}
          </span>
        </div>

        <form id="codemode-settings-form" onSubmit={handleSave}>
          {/* Master Switch */}
          <div
            className="form-group"
            style={{
              backgroundColor: 'rgba(255, 255, 255, 0.03)',
              padding: '16px',
              borderRadius: '8px',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              marginBottom: '20px',
            }}
          >
            <label style={{ display: 'flex', alignItems: 'center', gap: '12px', cursor: 'pointer', margin: 0 }}>
              <input
                type="checkbox"
                id="toggle-enable-code-mode"
                data-testid="toggle-enable-code-mode"
                checked={enableCodeMode}
                onChange={(e) => setEnableCodeMode(e.target.checked)}
                style={{ width: '20px', height: '20px', margin: 0, cursor: 'pointer' }}
              />
              <div>
                <strong style={{ fontSize: '14px', color: '#e2e8f0', display: 'block' }}>
                  Enable Sandboxed Scripting (JavaScript Tool Orchestrator)
                </strong>
                <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                  When enabled, the gateway advertises the <code>execute_script</code> meta-tool and exposes sandboxed V8 / Jint execution.
                </span>
              </div>
            </label>
          </div>

          {/* Execution Quotas & Guardrails */}
          <div className="form-group">
            <h3 style={{ fontSize: '14px', marginBottom: '14px', marginTop: '10px', color: 'var(--primary)' }}>
              <i className="fa-solid fa-gauge-high"></i> Execution Guardrails &amp; Quotas
            </h3>
          </div>

          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
            <div className="form-group">
              <label htmlFor="codeModeExecutionTimeoutSeconds" style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Execution Timeout (Seconds)</span>
                <strong style={{ color: 'var(--primary)' }}>{timeoutSeconds}s</strong>
              </label>
              <input
                type="range"
                min={1}
                max={120}
                id="codeModeExecutionTimeoutSeconds"
                data-testid="input-timeout"
                value={timeoutSeconds}
                onChange={(e) => setTimeoutSeconds(Number(e.target.value))}
                style={{ width: '100%' }}
              />
              <small style={{ color: 'var(--text-muted)', fontSize: '11px' }}>Maximum wall-clock execution time (1–120s).</small>
            </div>

            <div className="form-group">
              <label htmlFor="codeModeMaxToolCalls" style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Max Tool Calls per Script</span>
                <strong style={{ color: 'var(--primary)' }}>{maxToolCalls}</strong>
              </label>
              <input
                type="range"
                min={1}
                max={500}
                id="codeModeMaxToolCalls"
                data-testid="input-max-tool-calls"
                value={maxToolCalls}
                onChange={(e) => setMaxToolCalls(Number(e.target.value))}
                style={{ width: '100%' }}
              />
              <small style={{ color: 'var(--text-muted)', fontSize: '11px' }}>Maximum downstream backend tool invocations (1–500).</small>
            </div>
          </div>

          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '20px' }}>
            <div className="form-group">
              <label htmlFor="codeModeMaxMemoryMb" style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Memory Ceiling (MB)</span>
                <strong style={{ color: 'var(--primary)' }}>{maxMemoryMb} MB</strong>
              </label>
              <input
                type="range"
                min={16}
                max={512}
                step={16}
                id="codeModeMaxMemoryMb"
                data-testid="input-memory"
                value={maxMemoryMb}
                onChange={(e) => setMaxMemoryMb(Number(e.target.value))}
                style={{ width: '100%' }}
              />
              <small style={{ color: 'var(--text-muted)', fontSize: '11px' }}>Heap allocation limit for the JS engine (16–512 MB).</small>
            </div>

            <div className="form-group">
              <label htmlFor="codeModeMaxStatements" style={{ display: 'flex', justifyContent: 'space-between' }}>
                <span>Max Statements</span>
                <strong style={{ color: 'var(--primary)' }}>{maxStatements.toLocaleString()}</strong>
              </label>
              <input
                type="range"
                min={10000}
                max={1000000}
                step={10000}
                id="codeModeMaxStatements"
                data-testid="input-max-statements"
                value={maxStatements}
                onChange={(e) => setMaxStatements(Number(e.target.value))}
                style={{ width: '100%' }}
              />
              <small style={{ color: 'var(--text-muted)', fontSize: '11px' }}>Infinite loop guard: max JS statements (10k–1M).</small>
            </div>
          </div>

          {/* Access Control: Allowed Groups */}
          <div className="form-group">
            <h3 style={{ fontSize: '14px', marginBottom: '8px', marginTop: '10px', color: 'var(--primary)' }}>
              <i className="fa-solid fa-users-gear"></i> Access Control (Allowed Groups)
            </h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '12px', marginBottom: '12px' }}>
              Restrict script execution to specific security groups. If no groups are specified, all authenticated callers can execute scripts.
            </p>

            {/* Selected Group Chips */}
            <div
              style={{
                display: 'flex',
                flexWrap: 'wrap',
                gap: '8px',
                alignItems: 'center',
                padding: '10px',
                backgroundColor: 'rgba(0, 0, 0, 0.2)',
                borderRadius: '8px',
                border: '1px solid rgba(255, 255, 255, 0.08)',
                minHeight: '44px',
                marginBottom: '12px',
              }}
            >
              {allowedGroups.length === 0 ? (
                <span
                  className="badge badge-info"
                  style={{
                    backgroundColor: 'rgba(59, 130, 246, 0.15)',
                    color: '#93c5fd',
                    border: '1px solid rgba(59, 130, 246, 0.3)',
                    padding: '4px 10px',
                    borderRadius: '4px',
                    fontSize: '12px',
                  }}
                >
                  <i className="fa-solid fa-globe" style={{ marginRight: '6px' }}></i>
                  Allowed for all authenticated clients
                </span>
              ) : (
                allowedGroups.map((group) => (
                  <span
                    key={group}
                    className="badge"
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '6px',
                      backgroundColor: 'rgba(249, 115, 22, 0.15)',
                      color: 'var(--primary)',
                      border: '1px solid rgba(249, 115, 22, 0.35)',
                      padding: '4px 10px',
                      borderRadius: '16px',
                      fontSize: '12px',
                      fontWeight: 500,
                    }}
                  >
                    <span>{group}</span>
                    <button
                      type="button"
                      data-testid={`remove-group-${group}`}
                      onClick={() => handleRemoveGroup(group)}
                      style={{
                        background: 'transparent',
                        border: 'none',
                        color: 'inherit',
                        cursor: 'pointer',
                        padding: 0,
                        marginLeft: '4px',
                        fontSize: '14px',
                        lineHeight: '1',
                      }}
                      title={`Remove ${group}`}
                    >
                      &times;
                    </button>
                  </span>
                ))
              )}
            </div>

            {/* Add Group Controls */}
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <input
                type="text"
                data-testid="input-add-group"
                placeholder="Type group name and press Enter..."
                value={newGroupInput}
                onChange={(e) => setNewGroupInput(e.target.value)}
                onKeyDown={handleKeyDown}
                style={{ flex: 1 }}
              />
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={() => handleAddGroup()}
                style={{ whiteSpace: 'nowrap' }}
              >
                <i className="fa-solid fa-plus"></i> Add
              </button>

              {unselectedKnownGroups.length > 0 && (
                <select
                  data-testid="select-suggested-group"
                  value=""
                  onChange={(e) => {
                    if (e.target.value) {
                      handleAddGroup(e.target.value);
                    }
                  }}
                  style={{ maxWidth: '180px' }}
                >
                  <option value="" disabled>
                    Add known group...
                  </option>
                  {unselectedKnownGroups.map((g) => (
                    <option key={g} value={g}>
                      + {g}
                    </option>
                  ))}
                </select>
              )}
            </div>
          </div>

          {/* Syntax & Usage Preview Card */}
          <div
            className="syntax-preview-card"
            style={{
              marginTop: '24px',
              backgroundColor: 'rgba(0, 0, 0, 0.25)',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              borderRadius: '8px',
              padding: '16px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <i className="fa-brands fa-js" style={{ color: '#f7df1e', fontSize: '16px' }}></i>
              <strong style={{ fontSize: '13px', color: '#e2e8f0' }}>Sandboxed Scripting Environment</strong>
            </div>
            <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '12px' }}>
              Client agents can invoke <code>execute_script</code> passing JavaScript code. Downstream MCP backend tools are accessible via the <code>tools</code> proxy or <code>mcg.callTool</code>:
            </p>
            <pre
              style={{
                backgroundColor: 'rgba(15, 23, 42, 0.8)',
                padding: '12px',
                borderRadius: '6px',
                fontSize: '11px',
                fontFamily: 'monospace',
                color: '#cbd5e1',
                overflowX: 'auto',
                margin: 0,
                border: '1px solid rgba(255, 255, 255, 0.05)',
              }}
            >
{`// 1. Fluent proxy syntax (recommended)
const result = await tools.github.list_issues({ owner: "octocat", repo: "hello-world" });

// 2. Direct invocation syntax
const status = await mcg.callTool("docker__list_containers", {});

// Return orchestrated outcome directly to the agent
return { count: result.length, status };`}
            </pre>
          </div>

          {/* Action Footer */}
          <div className="settings-actions" style={{ marginTop: '24px', display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
            <button
              type="submit"
              data-testid="btn-save-code-mode"
              className="btn btn-primary"
              disabled={isSaving}
              style={{ minWidth: '130px' }}
            >
              {isSaving ? (
                <>
                  <i className="fa-solid fa-spinner fa-spin"></i> Saving...
                </>
              ) : saveStatus === 'saved' ? (
                <>
                  <i className="fa-solid fa-check"></i> Saved
                </>
              ) : (
                <>
                  <i className="fa-solid fa-floppy-disk"></i> Save Settings
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
