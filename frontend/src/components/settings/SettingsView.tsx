import React, { useEffect, useState } from 'react';
import { useSettingsStore } from '../../stores/useSettingsStore';

import { GeneralTab } from './GeneralTab';
import { IdentityAuthTab } from './IdentityAuthTab';
import { SecretProvidersTab } from './SecretProvidersTab';
import { CustomFilesTab } from './CustomFilesTab';
import { AccessControlTab } from './AccessControlTab';

export const SettingsView: React.FC = () => {
  const [activeSubview, setActiveSubview] = useState<'search' | 'identity' | 'secrets' | 'files' | 'permissions'>('search');

  const {
    embeddingSettings,
    authProviders,
    secretProviders,
    customFiles,
    policies,
    mappings,
    fetchEmbeddingSettings,
    saveEmbeddingSettings,
    setMasterKey,
    fetchProviders,
    saveAuthProvider,
    saveSecretProvider,
    fetchCustomFiles,
    openCustomFileModal,
    deleteCustomFile,
    fetchPolicies,
    openPolicyModal,
    deletePolicy,
    fetchMappings,
    openMappingModal,
    deleteMapping,
  } = useSettingsStore();

  // Initial load
  useEffect(() => {
    fetchEmbeddingSettings();
    fetchProviders();
    fetchCustomFiles();
    fetchPolicies();
    fetchMappings();
  }, [fetchEmbeddingSettings, fetchProviders, fetchCustomFiles, fetchPolicies, fetchMappings]);

  return (
    <div id="view-settings" className="view-panel active">
      <div
        className="tester-tabs settings-sub-nav"
        style={{
          display: 'flex',
          justifyContent: 'center',
          flexWrap: 'wrap',
          gap: '15px',
          marginBottom: '25px',
          borderBottom: '1px solid var(--border-color)',
          paddingBottom: '10px',
          maxWidth: '800px',
          marginLeft: 'auto',
          marginRight: 'auto',
        }}
      >
        <button
          type="button"
          className={`tester-tab-btn settings-tab-btn ${activeSubview === 'search' ? 'active' : ''}`}
          onClick={() => setActiveSubview('search')}
        >
          <i className="fa-solid fa-brain"></i> Vector &amp; Search
        </button>
        <button
          type="button"
          className={`tester-tab-btn settings-tab-btn ${activeSubview === 'identity' ? 'active' : ''}`}
          onClick={() => setActiveSubview('identity')}
        >
          <i className="fa-solid fa-id-card"></i> Identity &amp; Auth
        </button>
        <button
          type="button"
          className={`tester-tab-btn settings-tab-btn ${activeSubview === 'secrets' ? 'active' : ''}`}
          onClick={() => setActiveSubview('secrets')}
        >
          <i className="fa-solid fa-vault"></i> Secret Providers
        </button>
        <button
          type="button"
          className={`tester-tab-btn settings-tab-btn ${activeSubview === 'files' ? 'active' : ''}`}
          onClick={() => setActiveSubview('files')}
        >
          <i className="fa-solid fa-folder-open"></i> Prompts &amp; Resources
        </button>
        <button
          type="button"
          className={`tester-tab-btn settings-tab-btn ${activeSubview === 'permissions' ? 'active' : ''}`}
          onClick={() => setActiveSubview('permissions')}
        >
          <i className="fa-solid fa-user-lock"></i> Access Control
        </button>
      </div>

      {/* Subview 1: Vector & Search */}
      {activeSubview === 'search' && (
        <GeneralTab
          key={embeddingSettings ? `${embeddingSettings.embeddingProvider}-${embeddingSettings.embeddingModelDir}-${embeddingSettings.embeddingApiUrl}-${embeddingSettings.masterKeySource}` : 'loading'}
          settings={embeddingSettings}
          saveEmbeddingSettings={saveEmbeddingSettings}
          setMasterKey={setMasterKey}
        />
      )}

      {/* Subview 2: Identity & Auth */}
      {activeSubview === 'identity' && (
        <IdentityAuthTab
          key={(Array.isArray(authProviders) ? authProviders : []).map((p) => `${p.providerName}-${p.isEnabled}`).join(',')}
          providers={Array.isArray(authProviders) ? authProviders : []}
          saveAuthProvider={saveAuthProvider}
        />
      )}

      {/* Subview 3: Secret Providers */}
      {activeSubview === 'secrets' && (
        <SecretProvidersTab
          key={(Array.isArray(secretProviders) ? secretProviders : []).map((p) => `${p.providerName}-${p.isEnabled}`).join(',')}
          providers={Array.isArray(secretProviders) ? secretProviders : []}
          saveSecretProvider={saveSecretProvider}
        />
      )}

      {/* Subview 4: Prompts & Resources File Manager */}
      {activeSubview === 'files' && (
        <CustomFilesTab
          customFiles={customFiles}
          openCustomFileModal={openCustomFileModal}
          deleteCustomFile={deleteCustomFile}
        />
      )}

      {/* Subview 5: Access Control Policies */}
      {activeSubview === 'permissions' && (
        <AccessControlTab
          policies={policies}
          mappings={mappings}
          openPolicyModal={openPolicyModal}
          deletePolicy={deletePolicy}
          openMappingModal={openMappingModal}
          deleteMapping={deleteMapping}
        />
      )}
    </div>
  );
};
