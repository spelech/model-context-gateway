import React from 'react';

interface SemanticRouterCardProps {
  semanticQuery: string;
  semanticResults: any[];
  isSearchingSemantic: boolean;
  onQueryChange: (q: string) => void;
  onSearch: () => void;
}

export const SemanticRouterCard: React.FC<SemanticRouterCardProps> = ({
  semanticQuery,
  semanticResults,
  isSearchingSemantic,
  onQueryChange,
  onSearch,
}) => {
  return (
    <div className="glass-card">
      <h2>
        <i className="fa-solid fa-magnifying-glass-chart"></i> Semantic Router Simulator
      </h2>
      <div className="form-group">
        <label htmlFor="semantic-search-query">Natural Language Prompt</label>
        <input
          type="text"
          id="semantic-search-query"
          name="semantic-search-query"
          aria-label="Natural Language Prompt"
          placeholder="e.g. search matrix in plex"
          value={semanticQuery}
          onChange={(e) => onQueryChange(e.target.value)}
        />
      </div>
      <button type="button" className="btn btn-secondary" onClick={onSearch} disabled={isSearchingSemantic}>
        <i className="fa-solid fa-ranking-star"></i> {isSearchingSemantic ? 'Evaluating...' : 'Test Filter Score'}
      </button>

      <div className="semantic-search-results" id="semantic-search-results">
        {semanticResults.length === 0 ? (
          <div className="empty-state">
            {isSearchingSemantic ? 'Searching...' : 'Enter a prompt query to test tool matching scoring.'}
          </div>
        ) : (
          semanticResults.map((tool: any, idx: number) => (
            <div key={tool.name} className="search-result-item">
              <div className="search-result-header">
                <span className="search-result-name">{tool.name}</span>
                <span className="search-result-score">Rank #{idx + 1}</span>
              </div>
              <span className="search-result-desc">{tool.description || 'No description provided.'}</span>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
