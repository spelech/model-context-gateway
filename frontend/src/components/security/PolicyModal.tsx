import React, { useState } from 'react';
import { useSettingsStore } from '../../stores/useSettingsStore';

const PolicyModalDialog: React.FC = () => {
  const { editingPolicy, savePolicy, closePolicyModal } = useSettingsStore();

  const [targetId, setTargetId] = useState(editingPolicy?.targetId || '');
  const [requiredGroup, setRequiredGroup] = useState(editingPolicy?.requiredGroup || '');
  const [isAllowed, setIsAllowed] = useState(editingPolicy ? editingPolicy.isAllowed : true);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload: { id?: string; targetId: string; requiredGroup: string; isAllowed: boolean } = {
      targetId,
      requiredGroup,
      isAllowed,
    };
    if (editingPolicy) {
      payload.id = editingPolicy.id;
    }
    await savePolicy(payload);
  };

  return (
    <div className="modal-backdrop" id="policy-modal" data-testid="policy-modal" style={{ display: 'flex' }}>
      <div className="glass-card modal-card" style={{ maxWidth: '500px', width: '90%' }}>
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-shield-halved"></i> {editingPolicy ? 'Edit Access Policy' : 'Create Access Policy'}
          </h2>
          <button type="button" className="btn-close" data-testid="policy-close-btn" onClick={closePolicyModal}>
            &times;
          </button>
        </div>
        <form id="policy-form" onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="policy-target">Target ID</label>
            <input
              type="text"
              id="policy-target"
              data-testid="policy-target-input"
              placeholder="e.g. server:ha or tool:docker__list_containers"
              value={targetId}
              onChange={(e) => setTargetId(e.target.value)}
              required
            />
            <small style={{ color: 'var(--text-muted)', fontSize: '11px' }}>
              Use <code>server:ha</code>, <code>tool:plex__play</code>, <code>prompt:router__diagnose</code>, <code>resource:router://status</code>
            </small>
          </div>

          <div className="form-group">
            <label htmlFor="policy-group">Required Group / Internal Group</label>
            <input
              type="text"
              id="policy-group"
              data-testid="policy-group-input"
              placeholder="e.g. database_users or Administrators"
              value={requiredGroup}
              onChange={(e) => setRequiredGroup(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="policy-allowed">Policy Mode</label>
            <select
              id="policy-allowed"
              data-testid="policy-mode-select"
              value={isAllowed ? 'true' : 'false'}
              onChange={(e) => setIsAllowed(e.target.value === 'true')}
              required
            >
              <option value="true">ALLOW Access</option>
              <option value="false">DENY Access</option>
            </select>
          </div>

          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" data-testid="policy-cancel-btn" onClick={closePolicyModal}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" data-testid="policy-save-btn">
              Save Policy
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export const PolicyModal: React.FC = () => {
  const { isPolicyModalOpen, editingPolicy } = useSettingsStore();

  if (!isPolicyModalOpen) return null;

  return <PolicyModalDialog key={editingPolicy?.id || 'new'} />;
};
