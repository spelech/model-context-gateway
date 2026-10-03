import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SemanticRouterCard } from '../../components/testbench/SemanticRouterCard';
import { ToolSearchResultItem } from '../../shared/types';

describe('SemanticRouterCard Component', () => {
  const sampleResults: ToolSearchResultItem[] = [
    {
      tool: {
        name: 'docker__list_containers',
        description: 'List running Docker containers',
      },
      toolName: 'docker__list_containers',
      serverId: 'docker',
      score: 0.8765,
      denseScore: 0.9123,
      sparseScore: 0.8407,
      denseRank: 1,
      sparseRank: 2,
    },
    {
      tool: {
        name: 'native_calc',
        description: 'Perform basic math operations',
      },
      toolName: 'native_calc',
      score: 0.6543,
      denseScore: 0.7,
      sparseScore: 0.6,
      denseRank: 2,
      sparseRank: 3,
    },
  ];

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description verifies search mode toggle renders buttons and switches between hybrid, semantic, and lexical modes
   */
  it('verifies search mode toggle renders buttons and switches between hybrid, semantic, and lexical modes', () => {
    const onModeChange = vi.fn();

    const { rerender } = render(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={[]}
        isSearchingSemantic={false}
        searchMode="hybrid"
        denseWeight={0.5}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={onModeChange}
        onDenseWeightChange={vi.fn()}
        onSearchLimitChange={vi.fn()}
        onSearch={vi.fn()}
      />
    );

    const hybridBtn = screen.getByTestId('mode-btn-hybrid');
    const semanticBtn = screen.getByTestId('mode-btn-semantic');
    const lexicalBtn = screen.getByTestId('mode-btn-lexical');

    expect(hybridBtn).toBeInTheDocument();
    expect(semanticBtn).toBeInTheDocument();
    expect(lexicalBtn).toBeInTheDocument();

    expect(hybridBtn).toHaveClass('active');
    expect(semanticBtn).not.toHaveClass('active');
    expect(lexicalBtn).not.toHaveClass('active');

    fireEvent.click(semanticBtn);
    expect(onModeChange).toHaveBeenCalledWith('semantic');

    fireEvent.click(lexicalBtn);
    expect(onModeChange).toHaveBeenCalledWith('lexical');

    rerender(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={[]}
        isSearchingSemantic={false}
        searchMode="lexical"
        denseWeight={0.5}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={onModeChange}
        onDenseWeightChange={vi.fn()}
        onSearchLimitChange={vi.fn()}
        onSearch={vi.fn()}
      />
    );

    expect(hybridBtn).not.toHaveClass('active');
    expect(lexicalBtn).toHaveClass('active');
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description verifies hybrid split slider and quick preset chips appear in hybrid mode and update denseWeight
   */
  it('verifies hybrid split slider and quick preset chips appear in hybrid mode and update denseWeight', () => {
    const onDenseWeightChange = vi.fn();

    const { rerender } = render(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={[]}
        isSearchingSemantic={false}
        searchMode="hybrid"
        denseWeight={0.7}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={vi.fn()}
        onDenseWeightChange={onDenseWeightChange}
        onSearchLimitChange={vi.fn()}
        onSearch={vi.fn()}
      />
    );

    expect(screen.getByText(/Dense \/ Lexical Balance/i)).toBeInTheDocument();
    expect(screen.getByText(/Semantic 70% \/ Keyword 30%/i)).toBeInTheDocument();

    const slider = screen.getByTestId('hybrid-weight-slider');
    expect(slider).toBeInTheDocument();
    expect(slider).toHaveValue('0.7');

    fireEvent.change(slider, { target: { value: '0.4' } });
    expect(onDenseWeightChange).toHaveBeenCalledWith(0.4);

    // Test quick preset chips
    const presetBalanced = screen.getByTestId('preset-balanced');
    const presetSemanticBias = screen.getByTestId('preset-semantic-bias');
    const presetKeywordBias = screen.getByTestId('preset-keyword-bias');
    const presetPureSemantic = screen.getByTestId('preset-pure-semantic');
    const presetPureKeyword = screen.getByTestId('preset-pure-keyword');

    fireEvent.click(presetBalanced);
    expect(onDenseWeightChange).toHaveBeenCalledWith(0.5);

    fireEvent.click(presetSemanticBias);
    expect(onDenseWeightChange).toHaveBeenCalledWith(0.7);

    fireEvent.click(presetKeywordBias);
    expect(onDenseWeightChange).toHaveBeenCalledWith(0.3);

    fireEvent.click(presetPureSemantic);
    expect(onDenseWeightChange).toHaveBeenCalledWith(1.0);

    fireEvent.click(presetPureKeyword);
    expect(onDenseWeightChange).toHaveBeenCalledWith(0.0);

    // Verify slider & presets hidden in non-hybrid mode
    rerender(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={[]}
        isSearchingSemantic={false}
        searchMode="semantic"
        denseWeight={0.7}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={vi.fn()}
        onDenseWeightChange={onDenseWeightChange}
        onSearchLimitChange={vi.fn()}
        onSearch={vi.fn()}
      />
    );

    expect(screen.queryByTestId('hybrid-weight-slider')).not.toBeInTheDocument();
    expect(screen.queryByTestId('preset-balanced')).not.toBeInTheDocument();
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description verifies search limit dropdown changes value
   */
  it('verifies search limit dropdown changes value', () => {
    const onSearchLimitChange = vi.fn();

    render(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={[]}
        isSearchingSemantic={false}
        searchMode="hybrid"
        denseWeight={0.5}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={vi.fn()}
        onDenseWeightChange={vi.fn()}
        onSearchLimitChange={onSearchLimitChange}
        onSearch={vi.fn()}
      />
    );

    const limitSelect = screen.getByTestId('search-limit-select') as HTMLSelectElement;
    expect(limitSelect).toBeInTheDocument();
    expect(limitSelect.value).toBe('15');

    fireEvent.change(limitSelect, { target: { value: '25' } });
    expect(onSearchLimitChange).toHaveBeenCalledWith(25);
  });

  /**
   * @requirement UI-143
   * @category UI
   * @type PositiveFeature
   * @description renders decomposed score badges with formatted percentages in hybrid mode
   */
  it('renders decomposed score badges with formatted percentages in hybrid mode', () => {
    render(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={sampleResults}
        isSearchingSemantic={false}
        searchMode="hybrid"
        denseWeight={0.5}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={vi.fn()}
        onDenseWeightChange={vi.fn()}
        onSearchLimitChange={vi.fn()}
        onSearch={vi.fn()}
      />
    );

    const scoreTotals = screen.getAllByTestId('score-total');
    expect(scoreTotals).toHaveLength(2);
    expect(scoreTotals[0]).toHaveTextContent('Score: 87.6%');
    expect(scoreTotals[1]).toHaveTextContent('Score: 65.4%');

    const scoreDenses = screen.getAllByTestId('score-dense');
    expect(scoreDenses).toHaveLength(2);
    expect(scoreDenses[0]).toHaveTextContent('Semantic: 91.2%');
    expect(scoreDenses[1]).toHaveTextContent('Semantic: 70.0%');

    const scoreSparses = screen.getAllByTestId('score-sparse');
    expect(scoreSparses).toHaveLength(2);
    expect(scoreSparses[0]).toHaveTextContent('Keyword: 84.1%');
    expect(scoreSparses[1]).toHaveTextContent('Keyword: 60.0%');

    // Server badge when present
    expect(screen.getByText('docker')).toHaveClass('badge', 'badge-server');
  });

  /**
   * @requirement UI-143
   * @category UI
   * @type PositiveFeature
   * @description hides dense and sparse badges in pure semantic or lexical modes
   */
  it('hides dense and sparse badges in pure semantic or lexical modes', () => {
    render(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={sampleResults}
        isSearchingSemantic={false}
        searchMode="semantic"
        denseWeight={1.0}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={vi.fn()}
        onDenseWeightChange={vi.fn()}
        onSearchLimitChange={vi.fn()}
        onSearch={vi.fn()}
      />
    );

    expect(screen.getAllByTestId('score-total')).toHaveLength(2);
    expect(screen.queryByTestId('score-dense')).not.toBeInTheDocument();
    expect(screen.queryByTestId('score-sparse')).not.toBeInTheDocument();
  });

  /**
   * @requirement UI-143
   * @category UI
   * @type PositiveFeature
   * @description clicking Test Tool button dispatches onSelectTool with serverId and toolName
   */
  it('clicking Test Tool button dispatches onSelectTool with serverId and toolName', () => {
    const onSelectTool = vi.fn();

    render(
      <SemanticRouterCard
        semanticQuery="search containers"
        semanticResults={sampleResults}
        isSearchingSemantic={false}
        searchMode="hybrid"
        denseWeight={0.5}
        searchLimit={15}
        onQueryChange={vi.fn()}
        onModeChange={vi.fn()}
        onDenseWeightChange={vi.fn()}
        onSearchLimitChange={vi.fn()}
        onSearch={vi.fn()}
        onSelectTool={onSelectTool}
      />
    );

    const testToolBtn1 = screen.getByTestId('test-tool-btn-docker__list_containers');
    expect(testToolBtn1).toBeInTheDocument();

    fireEvent.click(testToolBtn1);
    expect(onSelectTool).toHaveBeenCalledWith('docker', 'docker__list_containers');

    const testToolBtn2 = screen.getByTestId('test-tool-btn-native_calc');
    expect(testToolBtn2).toBeInTheDocument();

    fireEvent.click(testToolBtn2);
    expect(onSelectTool).toHaveBeenCalledWith('', 'native_calc');
  });
});
