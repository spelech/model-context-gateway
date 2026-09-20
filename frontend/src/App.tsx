import React, { useState } from 'react';
import { Header, Footer, Toasts, ConfirmModal } from './components/shared';
import { DashboardView, ServerModal, ServerInspectModal } from './components/servers';
import { SecurityView, PolicyModal, MappingModal } from './components/security';
import { TestBenchView } from './components/testbench';
import { SettingsView, CustomFileModal } from './components/settings';
import { MyMcpServers } from './pages/MyMcpServers';
import { ConsentView } from './pages/ConsentView';
import { ClientModal, AppKeyModal } from './components/clients';
import { useUserStore } from './stores/useUserStore';

const App: React.FC = () => {
  const [currentView, setCurrentView] = useState<'dashboard' | 'security' | 'testbench' | 'settings' | 'my-mcp-servers'>(() => {
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      if (params.has('connected') || window.location.pathname.includes('my-servers')) {
        return 'my-mcp-servers';
      }
    }
    return 'dashboard';
  });
  const { user } = useUserStore();
  const isAdmin = !!(user?.groups && user.groups.includes('full_admin'));

  if (window.location.pathname === '/consent') {
    return <ConsentView />;
  }

  return (
    <>
      <div className="dashboard-container">
        <Header />

        <nav className="tabs-nav">
          <button
            className={`tab-btn ${currentView === 'dashboard' ? 'active' : ''}`}
            onClick={() => setCurrentView('dashboard')}
          >
            <i className="fa-solid fa-gauge"></i> Overview
          </button>
          <button
            className={`tab-btn ${currentView === 'security' ? 'active' : ''}`}
            onClick={() => setCurrentView('security')}
          >
            <i className="fa-solid fa-key"></i> {isAdmin ? 'App Keys & Security' : 'My App Keys'}
          </button>
          <button
            className={`tab-btn ${currentView === 'testbench' ? 'active' : ''}`}
            onClick={() => setCurrentView('testbench')}
          >
            <i className="fa-solid fa-vial"></i> Test Bench
          </button>
          {isAdmin && (
            <button
              className={`tab-btn ${currentView === 'settings' ? 'active' : ''}`}
              onClick={() => setCurrentView('settings')}
            >
              <i className="fa-solid fa-gear"></i> Settings
            </button>
          )}
          <button
            className={`tab-btn ${currentView === 'my-mcp-servers' ? 'active' : ''}`}
            onClick={() => setCurrentView('my-mcp-servers')}
          >
            <i className="fa-solid fa-server"></i> My MCP Servers
          </button>
        </nav>

        {currentView === 'dashboard' && <DashboardView />}
        {currentView === 'security' && <SecurityView />}
        {currentView === 'testbench' && <TestBenchView />}
        {currentView === 'settings' && isAdmin && <SettingsView />}
        {currentView === 'my-mcp-servers' && <MyMcpServers />}

        <Footer />
      </div>

      {/* Modals */}
      <ServerModal />
      <ServerInspectModal />
      {isAdmin && <ClientModal />}
      <AppKeyModal />
      <CustomFileModal />
      <PolicyModal />
      <MappingModal />
      <ConfirmModal />

      {/* Toast Manager */}
      <Toasts />
    </>
  );
};

export default App;
