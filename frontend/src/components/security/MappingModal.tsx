import React, { useState } from 'react';
import { useSettingsStore } from '../../stores/useSettingsStore';

const MappingModalDialog: React.FC = () => {
  const { editingMapping, saveMapping, closeMappingModal } = useSettingsStore();

  const [externalId, setExternalId] = useState(editingMapping?.externalId || '');
  const [internalGroup, setInternalGroup] = useState(editingMapping?.internalGroup || '');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const payload: { id?: string; externalId: string; internalGroup: string } = {
      externalId,
      internalGroup,
    };
    if (editingMapping) {
      payload.id = editingMapping.id;
    }
    await saveMapping(payload);
  };

  return (
    <div className="modal-backdrop" id="mapping-modal" data-testid="mapping-modal" style={{ display: 'flex' }}>
      <div className="glass-card modal-card" style={{ maxWidth: '500px', width: '90%' }}>
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-user-group"></i> {editingMapping ? 'Edit Group Mapping' : 'Create Group Mapping'}
          </h2>
          <button type="button" className="btn-close" data-testid="mapping-close-btn" onClick={closeMappingModal}>
            &times;
          </button>
        </div>
        <form id="mapping-form" onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="mapping-external">External AD SID or OIDC Group</label>
            <input
              type="text"
              id="mapping-external"
              data-testid="mapping-external-input"
              placeholder="e.g. S-1-5-21-... or devops_admins"
              value={externalId}
              onChange={(e) => setExternalId(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="mapping-internal">Internal Group Name</label>
            <input
              type="text"
              id="mapping-internal"
              data-testid="mapping-internal-input"
              placeholder="e.g. database_users"
              value={internalGroup}
              onChange={(e) => setInternalGroup(e.target.value)}
              required
            />
          </div>

          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" data-testid="mapping-cancel-btn" onClick={closeMappingModal}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" data-testid="mapping-save-btn">
              Save Mapping
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export const MappingModal: React.FC = () => {
  const { isMappingModalOpen, editingMapping } = useSettingsStore();

  if (!isMappingModalOpen) return null;

  return <MappingModalDialog key={editingMapping?.id || 'new'} />;
};
