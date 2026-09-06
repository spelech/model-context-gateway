import React, { useEffect } from 'react';
import { useServerStore, McpServer } from '../../stores/useServerStore';

import { StatsCard } from './StatsCard';
import { ClientSetupGuide } from '../clients/ClientSetupGuide';
import { ServerControlsToolbar } from './ServerControlsToolbar';
import { ServerCard } from './ServerCard';
import { PaginationToolbar } from '../shared/PaginationToolbar';

export const DashboardView: React.FC = () => {
  const {
    servers,
    searchQuery,
    sortBy,
    groupBy,
    currentPage,
    pageSize,
    collapsedGroups,
    fetchServers,
    setCurrentPage,
    setPageSize,
    toggleGroupCollapse,
  } = useServerStore();

  useEffect(() => {
    fetchServers();
    const serverPoll = setInterval(() => fetchServers(), 10000);

    return () => {
      clearInterval(serverPoll);
    };
  }, [fetchServers]);

  // Filter & Sort
  const filtered = servers.filter((s) => {
    if (!searchQuery) return true;
    const q = searchQuery.toLowerCase();
    const nameMatch = (s.displayName || '').toLowerCase().includes(q);
    const idMatch = (s.id || '').toLowerCase().includes(q);
    const aliasMatch = (s.alias || '').toLowerCase().includes(q);
    const urlMatch = (s.url || '').toLowerCase().includes(q);
    const catMatch = (s.categories || []).some((c) => (c || '').toLowerCase().includes(q));
    return nameMatch || idMatch || aliasMatch || urlMatch || catMatch;
  });

  filtered.sort((a, b) => {
    if (sortBy === 'name-asc') return (a.displayName || '').localeCompare(b.displayName || '');
    if (sortBy === 'name-desc') return (b.displayName || '').localeCompare(a.displayName || '');
    if (sortBy === 'type') return (a.type || '').localeCompare(b.type || '');
    if (sortBy === 'category') {
      const catA = a.categories?.[0] || 'Uncategorized';
      const catB = b.categories?.[0] || 'Uncategorized';
      return catA.localeCompare(catB);
    }
    // Default: status-priority
    const getPriority = (s: McpServer) => {
      if (!s.enabled) return 3;
      if (s.connectionStatus === 'Connected') return 2;
      return 1;
    };
    return getPriority(a) - getPriority(b);
  });

  const renderServerContent = () => {
    if (groupBy === 'none') {
      const totalItems = filtered.length;
      const effectivePageSize = pageSize === 'all' ? totalItems : pageSize;
      const totalPages = Math.ceil(totalItems / effectivePageSize);
      let activePage = currentPage;
      if (activePage > totalPages) activePage = Math.max(1, totalPages);

      const startIndex = (activePage - 1) * effectivePageSize;
      const endIndex = Math.min(startIndex + effectivePageSize, totalItems);
      const pageItems = filtered.slice(startIndex, endIndex);

      return (
        <>
          {pageItems.map((server) => (
            <ServerCard key={server.id} server={server} />
          ))}
          <PaginationToolbar
            currentPage={activePage}
            pageSize={pageSize}
            totalItems={totalItems}
            onPageChange={setCurrentPage}
            onPageSizeChange={setPageSize}
          />
        </>
      );
    } else {
      const groups: Record<string, McpServer[]> = {};
      filtered.forEach((server) => {
        let key = 'Uncategorized';
        if (groupBy === 'category') {
          key = server.categories && server.categories.length > 0 ? server.categories[0] : 'Uncategorized';
        } else if (groupBy === 'status') {
          key = server.enabled ? server.connectionStatus || 'Disconnected' : 'Disabled';
        } else if (groupBy === 'type') {
          key = (server.type || 'SSE').toUpperCase();
        }

        if (!groups[key]) groups[key] = [];
        groups[key].push(server);
      });

      const groupEntries = Object.entries(groups);

      return (
        <div className="server-groups-container">
          {groupEntries.map(([groupName, groupServers]) => {
            const isCollapsed = collapsedGroups.includes(groupName);

            return (
              <div key={groupName} className="server-group-section" style={{ marginBottom: '20px' }}>
                <div
                  className="server-group-header"
                  onClick={() => toggleGroupCollapse(groupName)}
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: '8px 12px',
                    background: 'rgba(255,255,255,0.03)',
                    border: '1px solid var(--border-color)',
                    borderRadius: '6px',
                    cursor: 'pointer',
                    userSelect: 'none',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', fontWeight: 600 }}>
                    <i className={`fa-solid fa-chevron-${isCollapsed ? 'right' : 'down'}`} style={{ fontSize: '11px', color: 'var(--text-muted)' }}></i>
                    <span>{groupName}</span>
                    <span className="server-badge" style={{ fontSize: '11px' }}>{groupServers.length}</span>
                  </div>
                </div>

                {!isCollapsed && (
                  <div className="server-group-items" style={{ marginTop: '10px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {groupServers.map((server) => (
                      <ServerCard key={server.id} server={server} />
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      );
    }
  };

  return (
    <div id="view-dashboard" className="view-panel active">
      <StatsCard />

      <div className="glass-card" style={{ marginTop: '20px' }}>
        <h2>
          <i className="fa-solid fa-server"></i> Backend MCP Servers
        </h2>
        <ServerControlsToolbar />

        <div className="server-list" id="server-list" style={{ marginTop: '15px' }}>
          {filtered.length === 0 ? (
            <div className="empty-state" style={{ padding: '30px', textAlign: 'center', color: 'var(--text-muted)' }}>
              No MCP servers matching your filters.
            </div>
          ) : (
            renderServerContent()
          )}
        </div>
      </div>

      <div style={{ marginTop: '25px' }}>
        <ClientSetupGuide />
      </div>
    </div>
  );
};
