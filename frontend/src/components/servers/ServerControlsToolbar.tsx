import React from 'react';
import { useServerStore } from '../../stores/useServerStore';

export const ServerControlsToolbar: React.FC = () => {
  const {
    searchQuery,
    sortBy,
    groupBy,
    setSearchQuery,
    setSortBy,
    setGroupBy,
    fetchServers,
    openAddModal,
  } = useServerStore();

  return (
    <div className="server-controls-toolbar" data-testid="server-controls-toolbar">
      <div className="search-box">
        <i className="fa-solid fa-magnifying-glass search-icon"></i>
        <input
          type="text"
          id="server-search"
          data-testid="server-search-input"
          aria-label="Filter servers by name, url, or category"
          placeholder="Filter servers by name, url, or category..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
        />
        {searchQuery && (
          <button
            className="btn-icon btn-clear-search"
            data-testid="server-search-clear-btn"
            onClick={() => setSearchQuery('')}
            title="Clear search"
          >
            <i className="fa-solid fa-xmark"></i>
          </button>
        )}
      </div>

      <div className="toolbar-actions">
        <div className="select-group">
          <label htmlFor="server-sort-by">Sort:</label>
          <select
            id="server-sort-by"
            data-testid="server-sort-select"
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value)}
          >
            <option value="status-priority">Status Priority</option>
            <option value="name-asc">Name (A-Z)</option>
            <option value="name-desc">Name (Z-A)</option>
            <option value="type">Type</option>
            <option value="category">Category</option>
          </select>
        </div>

        <div className="select-group">
          <label htmlFor="server-group-by">Group:</label>
          <select
            id="server-group-by"
            data-testid="server-group-select"
            value={groupBy}
            onChange={(e) => setGroupBy(e.target.value)}
          >
            <option value="none">None</option>
            <option value="category">Category</option>
            <option value="status">Status</option>
            <option value="type">Type</option>
          </select>
        </div>

        <button
          className="btn btn-secondary btn-sm"
          id="btn-refresh-all"
          data-testid="btn-refresh-all"
          title="Reconnect and refresh all servers"
          onClick={() => fetchServers(true)}
        >
          <i className="fa-solid fa-arrows-rotate"></i> Refresh All
        </button>

        <button
          className="btn btn-primary btn-sm"
          id="btn-add-server"
          data-testid="btn-add-server"
          onClick={openAddModal}
        >
          <i className="fa-solid fa-plus"></i> Add Server
        </button>
      </div>
    </div>
  );
};
