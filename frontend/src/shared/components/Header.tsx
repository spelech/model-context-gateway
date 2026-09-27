import React, { useEffect, useState } from 'react';
import { useUserStore } from '../../stores/useUserStore';
import { useConfigStore } from '../../stores/useConfigStore';
import { isImageUrl, updateFaviconAndTitle } from '../utils/branding';

export const Header: React.FC = () => {
  const { user, version, loadUser, loadVersion } = useUserStore();
  const { branding, loadBranding } = useConfigStore();
  const [theme, setTheme] = useState<'light' | 'dark'>(() => {
    return (typeof window !== 'undefined' && (localStorage.getItem('mcp-theme') as 'light' | 'dark')) || 'dark';
  });

  useEffect(() => {
    loadUser();
    loadVersion();
    loadBranding();
  }, [loadUser, loadVersion, loadBranding]);

  useEffect(() => {
    updateFaviconAndTitle(branding?.title, branding?.icon);
  }, [branding?.title, branding?.icon]);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

  const toggleTheme = () => {
    const nextTheme = theme === 'light' ? 'dark' : 'light';
    setTheme(nextTheme);
    document.documentElement.setAttribute('data-theme', nextTheme);
    localStorage.setItem('mcp-theme', nextTheme);
  };

  const isAdmin = user?.groups && user.groups.includes('full_admin');
  const groupText = isAdmin ? 'Admin' : 'User';

  return (
    <header className="dashboard-header" data-testid="dashboard-header">
      <div className="header-logo" data-testid="header-branding">
        {branding?.icon && isImageUrl(branding.icon) ? (
          <img src={branding.icon} alt="Logo" className="logo-icon logo-img" />
        ) : (
          <i className={`${branding?.icon || 'fa-solid fa-network-wired'} logo-icon`}></i>
        )}
        <div className="header-title">
          <div className="header-title-main" style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
            <h1>{branding?.title || 'Model Context Gateway'}</h1>
            <span className="badge badge-secondary" id="mcg-badge" data-testid="badge-mcg">
              MCG
            </span>
            <span className="badge badge-primary" id="version-badge" data-testid="badge-version">
              v{version}
            </span>
          </div>
          <span className="header-subtitle" style={{ fontSize: 'var(--font-size-xs)', color: 'var(--text-muted)' }}>
            High-Performance MCP Aggregator &amp; Semantic Gateway
          </span>
        </div>
      </div>
      <div className="header-status">
        <div className="status-item">
          <span className="label">API Gateway Status</span>
          <span className="value">
            <span className="indicator online"></span> Online
          </span>
        </div>
        <div className="status-item">
          <span className="label">Endpoint</span>
          <span className="value code">{window.location.origin}/sse</span>
        </div>
        {user?.authenticated && (
          <div className="status-item" id="user-status-item" data-testid="user-status-item">
            <span className="label">User Session</span>
            <span className="value" id="user-display" data-testid="user-display">
              {isAdmin ? (
                <i className="fa-solid fa-user-shield" style={{ color: 'var(--accent)', marginRight: '4px' }}></i>
              ) : (
                <i className="fa-solid fa-user" style={{ marginRight: '4px' }}></i>
              )}
              {user.name || user.username} ({groupText})
            </span>
          </div>
        )}
        <div className="status-item" style={{ justifyContent: 'center', alignItems: 'center', display: 'flex' }}>
          <button id="theme-toggle" data-testid="theme-toggle-btn" className="btn-icon" onClick={toggleTheme} title="Toggle Light/Dark Mode">
            <i className={`fa-solid ${theme === 'light' ? 'fa-sun' : 'fa-moon'}`}></i>
          </button>
        </div>
      </div>
    </header>
  );
};
