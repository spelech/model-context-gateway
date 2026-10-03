import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useTestBenchState } from '../../components/testbench/useTestBenchState';
import * as testbenchApi from '../../api/testbenchApi';

describe('useTestBenchState', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(testbenchApi, 'fetchTestToolsApi').mockResolvedValue([]);
    vi.spyOn(testbenchApi, 'fetchTestPromptsApi').mockResolvedValue([]);
    vi.spyOn(testbenchApi, 'fetchTestResourcesApi').mockResolvedValue({ resources: [], templates: [] });
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description Initializes semantic search state with default mode, weight, limit, and empty results.
   */
  it('initializes semantic search state with defaults', async () => {
    const { result } = renderHook(() => useTestBenchState());

    await waitFor(() => {
      expect(result.current.tools).toEqual([]);
    });

    expect(result.current.searchMode).toBe('hybrid');
    expect(result.current.denseWeight).toBe(0.5);
    expect(result.current.searchLimit).toBe(15);
    expect(result.current.semanticResults).toEqual([]);
    expect(result.current.isSearchingSemantic).toBe(false);
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description Updates searchMode, denseWeight, and searchLimit via their respective state setters.
   */
  it('updates searchMode, denseWeight, and searchLimit state', async () => {
    const { result } = renderHook(() => useTestBenchState());

    await waitFor(() => {
      expect(result.current.tools).toEqual([]);
    });

    act(() => {
      result.current.setSearchMode('semantic');
      result.current.setDenseWeight(0.85);
      result.current.setSearchLimit(25);
    });

    expect(result.current.searchMode).toBe('semantic');
    expect(result.current.denseWeight).toBe(0.85);
    expect(result.current.searchLimit).toBe(25);
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description Executes semantic search using state parameters and updates semanticResults.
   */
  it('executes handleSemanticSearch using current state parameters', async () => {
    const mockResults = [
      {
        tool: { name: 'docker__run', description: 'Run container' },
        toolName: 'docker__run',
        serverId: 'docker',
        score: 0.91,
        denseScore: 0.95,
        sparseScore: 0.82,
        denseRank: 1,
        sparseRank: 2,
      },
    ];

    const searchSpy = vi.spyOn(testbenchApi, 'semanticSearchApi').mockResolvedValue({
      query: 'deploy container',
      mode: 'hybrid',
      denseWeight: 0.7,
      results: mockResults,
    });

    const { result } = renderHook(() => useTestBenchState());

    act(() => {
      result.current.setSemanticQuery('deploy container');
      result.current.setDenseWeight(0.7);
      result.current.setSearchLimit(10);
    });

    await act(async () => {
      await result.current.handleSemanticSearch();
    });

    expect(searchSpy).toHaveBeenCalledWith('deploy container', 'hybrid', 0.7, 10);
    expect(result.current.semanticResults).toEqual(mockResults);
    expect(result.current.isSearchingSemantic).toBe(false);
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type FailClosedGuardrail
   * @description Handles semantic search API errors gracefully by resetting results.
   */
  it('handles semantic search failures gracefully', async () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.spyOn(testbenchApi, 'semanticSearchApi').mockRejectedValue(new Error('Network error'));

    const { result } = renderHook(() => useTestBenchState());

    act(() => {
      result.current.setSemanticQuery('test error query');
    });

    await act(async () => {
      await result.current.handleSemanticSearch();
    });

    expect(result.current.semanticResults).toEqual([]);
    expect(result.current.isSearchingSemantic).toBe(false);
    expect(consoleSpy).toHaveBeenCalled();
  });
});
