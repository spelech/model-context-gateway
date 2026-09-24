import React from 'react';
import { AccessPolicy, GroupMapping } from '../../shared/types';

export interface AccessControlTabProps {
  policies: AccessPolicy[];
  mappings: GroupMapping[];
  openPolicyModal: (policy?: AccessPolicy) => void;
  deletePolicy: (id: string) => Promise<void>;
  openMappingModal: (mapping?: GroupMapping) => void;
  deleteMapping: (id: string) => Promise<void>;
}

export const AccessControlTab: React.FC<AccessControlTabProps> = ({
  policies,
  mappings,
  openPolicyModal,
  deletePolicy,
  openMappingModal,
  deleteMapping,
}) => {
  return (
    <div id="subview-permissions" className="settings-subview active">
      {/* Policies Card */}
      <div className="glass-card settings-card" style={{ maxWidth: '900px', margin: '0 auto 25px auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px' }}>
          <h2>
            <i className="fa-solid fa-shield-halved"></i> Access Control Policies
          </h2>
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => openPolicyModal()}>
            <i className="fa-solid fa-plus"></i> Create Policy
          </button>
        </div>
        <p className="description" style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '20px' }}>
          Define target-specific allow/deny rules for users based on their active directory or OIDC groups. TargetId format: <code>server:&lt;id&gt;</code>, <code>tool:&lt;name&gt;</code>, <code>prompt:&lt;name&gt;</code>, <code>resource:&lt;uri&gt;</code>.
        </p>
        <div className="table-container" style={{ overflowX: 'auto' }}>
          <table id="policies-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                <th style={{ padding: '10px' }}>Target ID</th>
                <th style={{ padding: '10px' }}>Required Group</th>
                <th style={{ padding: '10px' }}>Access</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {policies.length === 0 ? (
                <tr>
                  <td colSpan={4} className="empty-state" style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>
                    No policies configured. All targets are default-allowed.
                  </td>
                </tr>
              ) : (
                policies.map((p) => {
                  const badgeLabel = p.isAllowed ? 'ALLOW' : 'DENY';
                  const badgeStyle: React.CSSProperties = p.isAllowed
                    ? { background: 'rgba(16, 185, 129, 0.1)', color: '#10b981', border: '1px solid rgba(16, 185, 129, 0.2)', padding: '2px 6px', borderRadius: '4px', fontSize: '11px', fontWeight: '600' }
                    : { background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', border: '1px solid rgba(239, 68, 68, 0.2)', padding: '2px 6px', borderRadius: '4px', fontSize: '11px', fontWeight: '600' };

                  return (
                    <tr key={p.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <td style={{ padding: '12px 10px', fontFamily: 'monospace', fontWeight: 500 }}>{p.targetId}</td>
                      <td style={{ padding: '12px 10px' }}>{p.requiredGroup}</td>
                      <td style={{ padding: '12px 10px' }}>
                        <span style={badgeStyle}>{badgeLabel}</span>
                      </td>
                      <td style={{ padding: '12px 10px', textAlign: 'right' }}>
                        <button
                          className="btn btn-secondary btn-sm"
                          onClick={() => openPolicyModal(p)}
                          style={{ marginRight: '5px' }}
                        >
                          <i className="fa-solid fa-edit"></i> Edit
                        </button>
                        <button className="btn btn-danger btn-sm" onClick={() => deletePolicy(p.id!)}>
                          <i className="fa-solid fa-trash"></i> Delete
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Group/SID Mappings Card */}
      <div className="glass-card settings-card" style={{ maxWidth: '900px', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px' }}>
          <h2>
            <i className="fa-solid fa-user-group"></i> Group &amp; SID Mappings
          </h2>
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => openMappingModal()}>
            <i className="fa-solid fa-plus"></i> Create Mapping
          </button>
        </div>
        <p className="description" style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '20px' }}>
          Map external Active Directory SIDs or OIDC / SSO groups to internal virtual groups for easier access control.
        </p>
        <div className="table-container" style={{ overflowX: 'auto' }}>
          <table id="mappings-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                <th style={{ padding: '10px' }}>External Group / SID</th>
                <th style={{ padding: '10px' }}>Internal Group Name</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {mappings.length === 0 ? (
                <tr>
                  <td colSpan={3} className="empty-state" style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>
                    No mappings configured.
                  </td>
                </tr>
              ) : (
                mappings.map((m) => (
                  <tr key={m.id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                    <td style={{ padding: '12px 10px', fontFamily: 'monospace', fontWeight: 500 }}>{m.externalId}</td>
                    <td style={{ padding: '12px 10px' }}>{m.internalGroup}</td>
                    <td style={{ padding: '12px 10px', textAlign: 'right' }}>
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => openMappingModal(m)}
                        style={{ marginRight: '5px' }}
                      >
                        <i className="fa-solid fa-edit"></i> Edit
                      </button>
                      <button className="btn btn-danger btn-sm" onClick={() => deleteMapping(m.id!)}>
                        <i className="fa-solid fa-trash"></i> Delete
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
