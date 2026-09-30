import React, { useState } from 'react';
import { useAppKeyStore } from '../../stores/useAppKeyStore';
import { showToast } from '../../stores/useToastStore';
import { AppKeyConfigGenerator } from './AppKeyConfigGenerator';

export interface AppKeyRotateModalProps {
  isOpen: boolean;
  keyId: string;
  keyName: string;
  onClose: () => void;
}

export const AppKeyRotateModal: React.FC<AppKeyRotateModalProps> = ({
  isOpen,
  keyId,
  keyName,
  onClose,
}) => {
  const { rotateAppKey, isRotating } = useAppKeyStore();
  const [stage, setStage] = useState<'warning' | 'revealed'>('warning');
  const [rotatedResult, setRotatedResult] = useState<{
    id: string;
    name: string;
    keyPrefix: string;
    plaintextKey: string;
  } | null>(null);
  const [copiedKey, setCopiedKey] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  if (!isOpen) return null;

  const handleRotate = async () => {
    setSubmitting(true);
    try {
      const result = await rotateAppKey(keyId);
      setRotatedResult(result);
      setStage('revealed');
    } catch {
      // error handled in store
    } finally {
      setSubmitting(false);
    }
  };

  const copyPlaintextKey = async () => {
    if (rotatedResult?.plaintextKey) {
      try {
        if (navigator?.clipboard?.writeText) {
          await navigator.clipboard.writeText(rotatedResult.plaintextKey);
        }
        setCopiedKey(true);
        showToast('App Key copied to clipboard', 'success');
        setTimeout(() => setCopiedKey(false), 2000);
      } catch {
        showToast('Failed to copy key', 'error');
      }
    }
  };

  const handleSafeClose = () => {
    setStage('warning');
    setRotatedResult(null);
    onClose();
  };

  return (
    <div
      id="appkey-rotate-modal"
      data-testid="modal-backdrop"
      className="modal-backdrop"
      style={{ display: 'flex' }}
      onClick={(e) => {
        // Disables backdrop click dismissal to protect plaintext credentials
        e.stopPropagation();
      }}
    >
      <div
        className="glass-card modal-card"
        style={{ maxWidth: '560px' }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-arrows-rotate"></i> Rotate App Key
          </h2>
          {stage === 'warning' && (
            <button
              className="btn-close"
              data-testid="appkey-rotate-close-btn"
              onClick={handleSafeClose}
            >
              &times;
            </button>
          )}
        </div>

        {stage === 'warning' ? (
          <div>
            <div
              style={{
                background: 'rgba(239, 68, 68, 0.1)',
                border: '1px solid rgba(239, 68, 68, 0.4)',
                borderRadius: '8px',
                padding: '14px',
                marginBottom: '16px',
                display: 'flex',
                alignItems: 'flex-start',
                gap: '12px',
              }}
            >
              <i
                className="fa-solid fa-triangle-exclamation"
                style={{ color: '#ef4444', fontSize: '20px', marginTop: '2px' }}
              ></i>
              <div>
                <div style={{ fontWeight: 600, color: '#f87171', marginBottom: '4px' }}>
                  Warning: Immediate Invalidation
                </div>
                <p style={{ margin: 0, fontSize: '13px', color: '#e2e8f0', lineHeight: 1.5 }}>
                  Rotating this App Key will immediately invalidate the current credential. Any active clients, IDEs, or automated agents currently using this key will stop working until their configuration is updated.
                </p>
              </div>
            </div>

            <p style={{ fontSize: '13px', color: '#94a3b8', marginBottom: '16px' }}>
              Key to rotate: <strong style={{ color: '#fff' }}>{keyName}</strong>
            </p>

            <div className="modal-footer" style={{ marginTop: '20px' }}>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={handleSafeClose}
                disabled={submitting || isRotating}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-danger"
                style={{ background: '#dc2626', borderColor: '#ef4444' }}
                onClick={handleRotate}
                disabled={submitting || isRotating}
              >
                {submitting || isRotating ? 'Rotating...' : 'Confirm & Rotate Key'}
              </button>
            </div>
          </div>
        ) : (
          <div data-testid="appkey-rotate-revealed">
            <div
              style={{
                padding: '12px',
                background: 'rgba(249, 115, 22, 0.08)',
                border: '1px solid var(--accent)',
                borderRadius: '8px',
                marginBottom: '16px',
              }}
            >
              <h4 style={{ color: 'var(--accent)', margin: '0 0 8px 0', fontSize: '14px' }}>
                <i className="fa-solid fa-check-circle"></i> App Key Rotated Successfully!
              </h4>
              <p style={{ fontSize: '12px', margin: '0 0 10px 0', color: 'var(--secondary)' }}>
                Copy your new App Key now. It will <strong>never be shown again</strong>.
              </p>

              <div
                style={{
                  background: '#090d16',
                  padding: '10px 14px',
                  borderRadius: '6px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '10px',
                }}
              >
                <code
                  style={{
                    fontFamily: 'JetBrains Mono, monospace',
                    fontSize: '12px',
                    color: '#38bdf8',
                    wordBreak: 'break-all',
                  }}
                >
                  {rotatedResult?.plaintextKey}
                </code>
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  onClick={copyPlaintextKey}
                  title="Copy Key"
                >
                  {copiedKey ? <i className="fa-solid fa-check"></i> : <i className="fa-solid fa-copy"></i>}
                </button>
              </div>
            </div>

            {rotatedResult?.plaintextKey && (
              <AppKeyConfigGenerator plaintextKey={rotatedResult.plaintextKey} />
            )}

            <div style={{ marginTop: '20px' }}>
              <button
                type="button"
                className="btn btn-primary"
                onClick={handleSafeClose}
                style={{ width: '100%' }}
              >
                Close
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default AppKeyRotateModal;
