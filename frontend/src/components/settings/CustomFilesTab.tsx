import React from 'react';
import { CustomFileMeta } from '../../shared/types';

export interface CustomFilesTabProps {
  customFiles: CustomFileMeta[];
  openCustomFileModal: (meta?: CustomFileMeta) => Promise<void>;
  deleteCustomFile: (type: 'prompts' | 'resources', name: string) => Promise<void>;
}

export const CustomFilesTab: React.FC<CustomFilesTabProps> = ({
  customFiles,
  openCustomFileModal,
  deleteCustomFile,
}) => {
  return (
    <div id="subview-files" className="settings-subview active">
      <div className="glass-card settings-card" style={{ maxWidth: '800px', margin: '0 auto' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '15px' }}>
          <h2>
            <i className="fa-solid fa-folder-open"></i> Prompts &amp; Resources File Manager
          </h2>
          <button type="button" data-testid="btn-create-custom-file" className="btn btn-secondary btn-sm" onClick={() => openCustomFileModal()}>
            <i className="fa-solid fa-plus"></i> Create File
          </button>
        </div>
        <p className="description" style={{ color: 'var(--text-muted)', fontSize: '13px', marginBottom: '20px' }}>
          Manage your own local JSON prompts and markdown/text resources. They will be registered directly under the <code>router</code> namespace.
        </p>
        <div className="custom-files-table-container" style={{ overflowX: 'auto' }}>
          <table className="custom-files-table" style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', color: 'var(--text-muted)' }}>
                <th style={{ padding: '10px' }}>Type</th>
                <th style={{ padding: '10px' }}>Name</th>
                <th style={{ padding: '10px' }}>Size</th>
                <th style={{ padding: '10px' }}>Modified</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {customFiles.length === 0 ? (
                <tr>
                  <td colSpan={5} className="empty-state" style={{ padding: '20px', textAlign: 'center', color: 'var(--text-muted)' }}>
                    No custom files found.
                  </td>
                </tr>
              ) : (
                customFiles.map((file) => {
                  const formattedSize = (file.sizeBytes / 1024).toFixed(2) + ' KB';
                  const dateStr = new Date(file.lastModified).toLocaleString();
                  const typeLabel =
                    file.type === 'prompts' ? (
                      <span style={{ color: '#f59e0b' }}>
                        <i className="fa-solid fa-comments"></i> Prompt
                      </span>
                    ) : (
                      <span style={{ color: '#10b981' }}>
                        <i className="fa-solid fa-file-lines"></i> Resource
                      </span>
                    );

                  return (
                    <tr key={file.name} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <td style={{ padding: '12px 10px' }}>{typeLabel}</td>
                      <td style={{ padding: '12px 10px', fontFamily: 'monospace', fontWeight: 500 }}>{file.name}</td>
                      <td style={{ padding: '12px 10px', color: 'var(--text-muted)' }}>{formattedSize}</td>
                      <td style={{ padding: '12px 10px', color: 'var(--text-muted)' }}>{dateStr}</td>
                      <td style={{ padding: '12px 10px', textAlign: 'right' }}>
                        <button
                          className="btn btn-secondary btn-sm"
                          data-testid="btn-edit-custom-file"
                          onClick={() => openCustomFileModal(file)}
                          style={{ marginRight: '5px' }}
                        >
                          <i className="fa-solid fa-edit"></i> Edit
                        </button>
                        <button
                          className="btn btn-danger btn-sm"
                          data-testid="btn-delete-custom-file"
                          onClick={() => deleteCustomFile(file.type, file.name)}
                        >
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
    </div>
  );
};
