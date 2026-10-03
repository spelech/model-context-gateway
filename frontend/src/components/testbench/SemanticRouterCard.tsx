import React from 'react';
import { SearchMode, ToolSearchResultItem } from '../../shared/types';
import { FormattedDescription } from './FormattedDescription';

export interface SemanticRouterCardProps {
  semanticQuery: string;
  semanticResults: ToolSearchResultItem[];
  isSearchingSemantic: boolean;
  searchMode: SearchMode;
  denseWeight: number;
  searchLimit: number;
  onQueryChange: (q: string) => void;
  onModeChange: (mode: SearchMode) => void;
  onDenseWeightChange: (weight: number) => void;
  onSearchLimitChange: (limit: number) => void;
  onSearch: () => void;
  onSelectTool?: (serverId: string, toolName: string) => void;
}

export const SemanticRouterCard: React.FC<SemanticRouterCardProps> = ({
  semanticQuery,
  semanticResults,
  isSearchingSemantic,
  searchMode,
  denseWeight,
  searchLimit,
  onQueryChange,
  onModeChange,
  onDenseWeightChange,
  onSearchLimitChange,
  onSearch,
  onSelectTool,
}) => {
  return (
    <div className="glass-card" data-testid="semantic-router-card">
      <h2>
        <i className="fa-solid fa-magnifying-glass-chart"></i> Semantic Router Simulator
      </h2>

      {/* 3-way Search Mode Toggle */}
      <div className="search-mode-selector">
        <button
          type="button"
          className={`mode-toggle-btn ${searchMode === 'hybrid' ? 'active' : ''}`}
          data-testid="mode-btn-hybrid"
          onClick={() => onModeChange('hybrid')}
        >
          <i className="fa-solid fa-code-merge"></i> Hybrid Fusion
        </button>
        <button
          type="button"
          className={`mode-toggle-btn ${searchMode === 'semantic' ? 'active' : ''}`}
          data-testid="mode-btn-semantic"
          onClick={() => onModeChange('semantic')}
        >
          <i className="fa-solid fa-brain"></i> Semantic (Vector)
        </button>
        <button
          type="button"
          className={`mode-toggle-btn ${searchMode === 'lexical' ? 'active' : ''}`}
          data-testid="mode-btn-lexical"
          onClick={() => onModeChange('lexical')}
        >
          <i className="fa-solid fa-font"></i> Lexical (Keyword)
        </button>
      </div>

      {/* Hybrid Split Slider & Quick Presets (Only in Hybrid Mode) */}
      {searchMode === 'hybrid' && (
        <div className="hybrid-slider-container">
          <div className="hybrid-slider-header">
            <label htmlFor="hybrid-weight-slider">Dense / Lexical Balance</label>
            <span className="hybrid-slider-ratio">
              Semantic {Math.round(denseWeight * 100)}% / Keyword {Math.round((1 - denseWeight) * 100)}%
            </span>
          </div>
          <input
            id="hybrid-weight-slider"
            type="range"
            min={0}
            max={1}
            step={0.05}
            value={denseWeight}
            onChange={(e) => onDenseWeightChange(parseFloat(e.target.value))}
            data-testid="hybrid-weight-slider"
          />
          <div className="slider-presets">
            <button
              type="button"
              className={`preset-chip ${Math.abs(denseWeight - 0.5) < 0.01 ? 'active' : ''}`}
              data-testid="preset-balanced"
              onClick={() => onDenseWeightChange(0.5)}
            >
              Balanced (50/50)
            </button>
            <button
              type="button"
              className={`preset-chip ${Math.abs(denseWeight - 0.7) < 0.01 ? 'active' : ''}`}
              data-testid="preset-semantic-bias"
              onClick={() => onDenseWeightChange(0.7)}
            >
              Semantic Bias (70/30)
            </button>
            <button
              type="button"
              className={`preset-chip ${Math.abs(denseWeight - 0.3) < 0.01 ? 'active' : ''}`}
              data-testid="preset-keyword-bias"
              onClick={() => onDenseWeightChange(0.3)}
            >
              Keyword Bias (30/70)
            </button>
            <button
              type="button"
              className={`preset-chip ${Math.abs(denseWeight - 1.0) < 0.01 ? 'active' : ''}`}
              data-testid="preset-pure-semantic"
              onClick={() => onDenseWeightChange(1.0)}
            >
              Pure Semantic (100/0)
            </button>
            <button
              type="button"
              className={`preset-chip ${Math.abs(denseWeight - 0.0) < 0.01 ? 'active' : ''}`}
              data-testid="preset-pure-keyword"
              onClick={() => onDenseWeightChange(0.0)}
            >
              Pure Keyword (0/100)
            </button>
          </div>
        </div>
      )}

      {/* Query input and Limit selector */}
      <div className="semantic-query-controls">
        <div className="form-group query-input-group">
          <label htmlFor="semantic-search-query">Natural Language Prompt</label>
          <input
            type="text"
            id="semantic-search-query"
            data-testid="semantic-query-input"
            name="semantic-search-query"
            aria-label="Natural Language Prompt"
            placeholder="e.g. search matrix in plex"
            value={semanticQuery}
            onChange={(e) => onQueryChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                onSearch();
              }
            }}
          />
        </div>
        <div className="form-group limit-select-group">
          <label htmlFor="search-limit-select">Limit</label>
          <select
            id="search-limit-select"
            data-testid="search-limit-select"
            value={searchLimit}
            onChange={(e) => onSearchLimitChange(parseInt(e.target.value, 10))}
          >
            <option value={5}>5</option>
            <option value={10}>10</option>
            <option value={15}>15</option>
            <option value={25}>25</option>
          </select>
        </div>
      </div>

      <button
        type="button"
        className="btn btn-secondary"
        data-testid="semantic-search-btn"
        onClick={onSearch}
        disabled={isSearchingSemantic}
      >
        <i className="fa-solid fa-ranking-star"></i> {isSearchingSemantic ? 'Evaluating...' : 'Test Filter Score'}
      </button>

      {/* Search Hit Cards */}
      <div className="semantic-search-results" id="semantic-search-results" data-testid="semantic-results-container">
        {semanticResults.length === 0 ? (
          <div className="empty-state">
            {isSearchingSemantic ? 'Searching...' : 'Enter a prompt query to test tool matching scoring.'}
          </div>
        ) : (
          semanticResults.map((item: ToolSearchResultItem, idx: number) => {
            const toolName = item.toolName || item.tool?.name || (item as any).name || `tool-${idx}`;
            const serverId = item.serverId || (item as any).server || '';
            const desc = item.tool?.description || (item as any).description || '';
            const totalScore = typeof item.score === 'number' ? item.score : 0;

            return (
              <div key={`${toolName}-${idx}`} className="search-result-item">
                <div className="search-result-header">
                  <div className="search-result-title-group">
                    <span className="search-result-name">{toolName}</span>
                    {serverId && <span className="badge badge-server">{serverId}</span>}
                  </div>
                  <div className="search-result-scores">
                    <span className="badge badge-score-total" data-testid="score-total">
                      Score: {(totalScore * 100).toFixed(1)}%
                    </span>
                    {searchMode === 'hybrid' && (
                      <>
                        {typeof item.denseScore === 'number' && (
                          <span className="badge badge-score-dense" data-testid="score-dense">
                            Semantic: {(item.denseScore * 100).toFixed(1)}%
                          </span>
                        )}
                        {typeof item.sparseScore === 'number' && (
                          <span className="badge badge-score-sparse" data-testid="score-sparse">
                            Keyword: {(item.sparseScore * 100).toFixed(1)}%
                          </span>
                        )}
                      </>
                    )}
                  </div>
                </div>

                <div className="search-result-body">
                  {desc ? (
                    <FormattedDescription description={desc} text={desc} serverId={serverId} />
                  ) : (
                    <span className="search-result-desc">No description provided.</span>
                  )}
                </div>

                <div className="search-result-actions">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-primary test-tool-btn"
                    data-testid={`test-tool-btn-${toolName}`}
                    onClick={() => onSelectTool?.(serverId, toolName)}
                  >
                    <i className="fa-solid fa-play"></i> Test Tool
                  </button>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
};
