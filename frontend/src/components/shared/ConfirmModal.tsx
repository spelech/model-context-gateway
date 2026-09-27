import React from 'react';
import { Modal } from './Modal';
import { useConfirmStore } from '../../stores/useConfirmStore';

export const ConfirmModal: React.FC = () => {
  const { isOpen, options, handleConfirm, handleCancel } = useConfirmStore();

  if (!isOpen) return null;

  const titleNode = (
    <span style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
      {options.danger ? (
        <i className="fa-solid fa-triangle-exclamation" style={{ color: 'var(--status-offline)' }}></i>
      ) : (
        <i className="fa-solid fa-circle-question" style={{ color: 'var(--primary)' }}></i>
      )}
      {options.title || 'Confirm Action'}
    </span>
  );

  return (
    <Modal
      id="confirm-modal"
      isOpen={isOpen}
      onClose={handleCancel}
      title={titleNode}
      maxWidth="460px"
    >
      <div style={{ padding: '8px 0 20px 0', color: 'var(--text-main)', fontSize: 'var(--font-size-md)', lineHeight: '1.5' }}>
        <p style={{ margin: 0 }}>{options.message}</p>
      </div>
      <div className="modal-footer" style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
        <button
          type="button"
          className="btn btn-secondary"
          data-testid="confirm-modal-cancel-btn"
          onClick={handleCancel}
        >
          {options.cancelText || 'Cancel'}
        </button>
        <button
          type="button"
          className={`btn ${options.danger ? 'btn-danger' : 'btn-primary'}`}
          data-testid="confirm-modal-ok-btn"
          onClick={handleConfirm}
          autoFocus
        >
          {options.confirmText || 'Confirm'}
        </button>
      </div>
    </Modal>
  );
};
