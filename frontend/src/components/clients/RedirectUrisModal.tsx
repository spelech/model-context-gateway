import React from 'react';
import { Modal } from '../shared/Modal';
import { showToast } from '../../stores/useToastStore';

interface RedirectUrisModalProps {
  isOpen: boolean;
  onClose: () => void;
  clientName: string;
  redirectUris: string[];
}

export const RedirectUrisModal: React.FC<RedirectUrisModalProps> = ({
  isOpen,
  onClose,
  clientName,
  redirectUris,
}) => {
  const handleCopyUri = (uri: string) => {
    navigator.clipboard.writeText(uri);
    showToast('Redirect URI copied to clipboard', 'info');
  };

  const handleCopyAll = () => {
    navigator.clipboard.writeText(redirectUris.join('\n'));
    showToast(`Copied all ${redirectUris.length} redirect URIs to clipboard`, 'info');
  };

  return (
    <Modal
      id="modal-redirect-uris"
      isOpen={isOpen}
      onClose={onClose}
      title={
        <>
          <i className="fa-solid fa-link" style={{ marginRight: '8px', color: 'var(--accent)' }}></i>
          Redirect URIs &mdash; {clientName}
        </>
      }
      maxWidth="620px"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }} data-testid="redirect-uris-modal-content">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px' }}>
          <span className="badge badge-secondary" style={{ fontSize: '12px' }}>
            {redirectUris.length} Registered URI{redirectUris.length === 1 ? '' : 's'}
          </span>
          {redirectUris.length > 1 && (
            <button
              type="button"
              className="btn btn-secondary btn-sm"
              onClick={handleCopyAll}
              data-testid="btn-copy-all-uris"
              title="Copy all URIs as newline-separated list"
            >
              <i className="fa-solid fa-copy"></i> Copy All
            </button>
          )}
        </div>

        <div
          className="redirect-uris-list"
          style={{
            maxHeight: '360px',
            overflowY: 'auto',
            display: 'flex',
            flexDirection: 'column',
            gap: '8px',
            paddingRight: '4px',
          }}
        >
          {redirectUris.map((uri, idx) => (
            <div
              key={idx}
              className="redirect-uri-row"
              data-testid={`redirect-uri-row-${idx}`}
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: '12px',
                background: 'rgba(255, 255, 255, 0.04)',
                border: '1px solid var(--border-color)',
                borderRadius: 'var(--radius-sm)',
                padding: '8px 12px',
              }}
            >
              <span
                className="code"
                style={{
                  wordBreak: 'break-all',
                  fontSize: '12px',
                  flex: 1,
                  color: 'var(--text-main)',
                  fontFamily: 'var(--font-family-code)',
                }}
              >
                {uri}
              </span>
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                style={{ padding: '4px 8px', fontSize: '11px', flexShrink: 0 }}
                onClick={() => handleCopyUri(uri)}
                title="Copy URI"
                aria-label={`Copy redirect URI ${uri}`}
                data-testid={`btn-copy-uri-${idx}`}
              >
                <i className="fa-solid fa-copy"></i>
              </button>
            </div>
          ))}
        </div>

        <div className="modal-actions" style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '8px' }}>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={onClose}
            data-testid="btn-close-redirect-uris-modal"
          >
            Close
          </button>
        </div>
      </div>
    </Modal>
  );
};
