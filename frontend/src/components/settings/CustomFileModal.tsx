import React, { useState } from 'react';
import { useSettingsStore } from '../../stores/useSettingsStore';
import { showToast } from '../../stores/useToastStore';
import { VisualPromptBuilder, ArgBuilderItem, MsgBuilderItem } from './VisualPromptBuilder';


const initializePromptBuilder = (meta: { type: 'prompts' | 'resources'; name: string } | null, content: string) => {
  if (meta) {
    if (meta.type === 'prompts') {
      try {
        const parsed = JSON.parse(content);
        const desc = parsed.description || '';
        const args: ArgBuilderItem[] = Array.isArray(parsed.arguments)
          ? parsed.arguments.map((arg: { name?: string; description?: string; required?: boolean }) => ({
              id: Math.random().toString(),
              name: arg.name || '',
              description: arg.description || '',
              required: !!arg.required,
            }))
          : [];
        const msgs: MsgBuilderItem[] = Array.isArray(parsed.messages)
          ? parsed.messages.map((msg: { role?: string; content?: string | { text?: string } }) => {
              const text = msg.content && typeof msg.content === 'object' ? msg.content.text || '' : (msg.content || '');
              return {
                id: Math.random().toString(),
                role: msg.role === 'assistant' ? 'assistant' : 'user',
                text,
              };
            })
          : [];
        return { promptDesc: desc, builderArgs: args, builderMsgs: msgs };
      } catch {
        return { promptDesc: '', builderArgs: [], builderMsgs: [] };
      }
    }
    return { promptDesc: '', builderArgs: [], builderMsgs: [] };
  }
  return {
    promptDesc: 'My custom prompt description',
    builderArgs: [{ id: '1', name: 'topic', description: 'Topic to write about', required: true }],
    builderMsgs: [
      {
        id: '1',
        role: 'user' as const,
        text: 'Write a short summary about {{topic}}.',
      },
    ],
  };
};

const CustomFileModalDialog: React.FC = () => {
  const {
    editingFileMeta,
    editingFileContent,
    activeFileModalTab,
    saveCustomFile,
    closeCustomFileModal,
    setActiveFileModalTab,
  } = useSettingsStore();

  const [fileType, setFileType] = useState<'prompts' | 'resources'>(editingFileMeta?.type || 'prompts');
  const [fileName, setFileName] = useState(editingFileMeta?.name || '');
  const [rawContent, setRawContent] = useState(editingFileContent);

  const initialPromptData = initializePromptBuilder(editingFileMeta, editingFileContent);
  const [promptDesc, setPromptDesc] = useState(initialPromptData.promptDesc);
  const [builderArgs, setBuilderArgs] = useState<ArgBuilderItem[]>(initialPromptData.builderArgs);
  const [builderMsgs, setBuilderMsgs] = useState<MsgBuilderItem[]>(initialPromptData.builderMsgs);

  const compileBuilderToJson = (): string => {
    const promptObj = {
      description: promptDesc,
      arguments: builderArgs
        .filter((arg) => arg.name.trim() !== '')
        .map((arg) => ({
          name: arg.name.trim(),
          description: arg.description.trim(),
          required: arg.required,
        })),
      messages: builderMsgs.map((msg) => ({
        role: msg.role,
        content: {
          type: 'text',
          text: msg.text,
        },
      })),
    };
    return JSON.stringify(promptObj, null, 2);
  };

  const handleTabSwitch = (tab: 'editor' | 'builder') => {
    if (tab === 'editor' && activeFileModalTab === 'builder' && fileType === 'prompts') {
      // Sync builder -> raw JSON
      setRawContent(compileBuilderToJson());
    } else if (tab === 'builder' && activeFileModalTab === 'editor' && fileType === 'prompts') {
      // Sync raw JSON -> builder
      try {
        const parsed = JSON.parse(rawContent);
        setPromptDesc(parsed.description || '');
        if (Array.isArray(parsed.arguments)) {
          setBuilderArgs(
            parsed.arguments.map((arg: any) => ({
              id: Math.random().toString(),
              name: arg.name || '',
              description: arg.description || '',
              required: !!arg.required,
            }))
          );
        }
        if (Array.isArray(parsed.messages)) {
          setBuilderMsgs(
            parsed.messages.map((msg: any) => ({
              id: Math.random().toString(),
              role: msg.role === 'assistant' ? 'assistant' : 'user',
              text: msg.content && typeof msg.content === 'object' ? msg.content.text || '' : (msg.content || ''),
            }))
          );
        }
      } catch {
        showToast('Cannot switch to Visual Builder: JSON in editor is invalid.', 'error');
        return;
      }
    }
    setActiveFileModalTab(tab);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!fileName.trim()) {
      showToast('Please enter a file name.', 'error');
      return;
    }

    let finalContent = rawContent;
    if (fileType === 'prompts' && activeFileModalTab === 'builder') {
      finalContent = compileBuilderToJson();
    }

    // Ensure valid JSON if prompt
    if (fileType === 'prompts') {
      try {
        JSON.parse(finalContent);
      } catch {
        showToast('Invalid JSON content. Please check syntax or use the Visual Builder.', 'error');
        return;
      }
    }

    const success = await saveCustomFile(fileType, fileName.trim(), finalContent);
    if (success) {
      closeCustomFileModal();
    }
  };

  return (
    <div className="modal-backdrop" id="custom-file-modal" data-testid="custom-file-modal" style={{ display: 'flex' }}>
      <div className="glass-card modal-card" style={{ maxWidth: '850px', width: '90%' }}>
        <div className="modal-header">
          <h2>
            <i className="fa-solid fa-file-code"></i> {editingFileMeta ? `Edit ${editingFileMeta.name}` : 'Create Custom File'}
          </h2>
          <button type="button" className="btn-close" data-testid="custom-file-close-btn" onClick={closeCustomFileModal}>
            &times;
          </button>
        </div>

        <form id="custom-file-form" onSubmit={handleSave}>
          <div className="form-row" style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '15px' }}>
            <div className="form-group">
              <label htmlFor="custom-file-type">File Type</label>
              <select
                id="custom-file-type"
                data-testid="custom-file-type-select"
                value={fileType}
                disabled={!!editingFileMeta}
                onChange={(e) => {
                  const newType = e.target.value as 'prompts' | 'resources';
                  setFileType(newType);
                  if (newType === 'prompts' && !fileName.endsWith('.json')) {
                    setFileName((prev) => prev.replace(/\.[^/.]+$/, '') + '.json');
                  } else if (newType === 'resources' && !fileName.endsWith('.md') && !fileName.endsWith('.txt')) {
                    setFileName((prev) => prev.replace(/\.[^/.]+$/, '') + '.md');
                  }
                }}
              >
                <option value="prompts">Prompt (JSON)</option>
                <option value="resources">Resource (Markdown/Text)</option>
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="custom-file-name">File Name</label>
              <input
                type="text"
                id="custom-file-name"
                data-testid="custom-file-name-input"
                placeholder={fileType === 'prompts' ? 'e.g. system-summary.json' : 'e.g. guide.md'}
                value={fileName}
                disabled={!!editingFileMeta}
                onChange={(e) => setFileName(e.target.value)}
                required
              />
            </div>
          </div>

          {fileType === 'prompts' && (
            <div className="tester-tabs" style={{ marginBottom: '15px', marginTop: '5px' }}>
              <button
                type="button"
                data-testid="custom-file-tab-editor"
                className={`tester-tab-btn ${activeFileModalTab === 'editor' ? 'active' : ''}`}
                onClick={() => handleTabSwitch('editor')}
              >
                <i className="fa-solid fa-code"></i> Raw JSON Editor
              </button>
              <button
                type="button"
                data-testid="custom-file-tab-builder"
                className={`tester-tab-btn ${activeFileModalTab === 'builder' ? 'active' : ''}`}
                onClick={() => handleTabSwitch('builder')}
              >
                <i className="fa-solid fa-wand-magic-sparkles"></i> Visual Prompt Builder
              </button>
            </div>
          )}

          {activeFileModalTab === 'editor' || fileType === 'resources' ? (
            <div className="form-group">
              <label htmlFor="custom-file-content">File Content</label>
              <textarea
                id="custom-file-content"
                data-testid="custom-file-content-input"
                rows={14}
                style={{
                  fontFamily: 'JetBrains Mono, monospace',
                  fontSize: '13px',
                  background: 'var(--bg-dark)',
                  color: 'var(--text-main)',
                  border: '1px solid var(--border-color)',
                  width: '100%',
                  padding: '10px',
                  borderRadius: '6px',
                }}
                value={rawContent}
                onChange={(e) => setRawContent(e.target.value)}
                required
              />
            </div>
          ) : (
                        <VisualPromptBuilder
              promptDesc={promptDesc}
              setPromptDesc={setPromptDesc}
              builderArgs={builderArgs}
              setBuilderArgs={setBuilderArgs}
              builderMsgs={builderMsgs}
              setBuilderMsgs={setBuilderMsgs}
            />
          )}

          <div className="modal-footer" style={{ marginTop: '15px' }}>
            <button type="button" className="btn btn-secondary" data-testid="custom-file-cancel-btn" onClick={closeCustomFileModal}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" id="btn-save-custom-file" data-testid="custom-file-save-btn">
              Save File
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export const CustomFileModal: React.FC = () => {
  const { isCustomFileOpen, editingFileMeta } = useSettingsStore();

  if (!isCustomFileOpen) return null;

  return <CustomFileModalDialog key={editingFileMeta ? `${editingFileMeta.type}-${editingFileMeta.name}` : 'new'} />;
};
