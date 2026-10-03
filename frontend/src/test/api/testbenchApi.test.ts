import { describe, it, expect, vi, beforeEach } from 'vitest';
import { semanticSearchApi } from '../../api/testbenchApi';
import * as api from '../../shared/api/api';
import { SemanticSearchResponse } from '../../shared/types/testbench';

describe('testbenchApi', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description Verifies semantic search API client passes searchMode, denseWeight, and limit to backend.
   */
  it('calls apiRequest with query, mode, denseWeight, and limit parameters', async () => {
    const mockResponse: SemanticSearchResponse = {
      query: 'docker list containers',
      mode: 'hybrid',
      denseWeight: 0.7,
      results: [
        {
          tool: { name: 'docker__list_containers', description: 'List containers' },
          toolName: 'docker__list_containers',
          serverId: 'docker',
          score: 0.88,
          denseScore: 0.92,
          sparseScore: 0.81,
          denseRank: 1,
          sparseRank: 2,
        },
      ],
    };

    const spy = vi.spyOn(api, 'apiRequest').mockResolvedValue(mockResponse);

    const result = await semanticSearchApi('docker list containers', 'hybrid', 0.7, 10);

    expect(spy).toHaveBeenCalledWith('/api/test/semantic-search', {
      method: 'POST',
      body: {
        query: 'docker list containers',
        mode: 'hybrid',
        denseWeight: 0.7,
        limit: 10,
      },
    });

    expect(result).toEqual(mockResponse);
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description Verifies semantic search API client uses default arguments for mode, denseWeight, and limit.
   */
  it('uses default values for mode, denseWeight, and limit when not specified', async () => {
    const mockResponse: SemanticSearchResponse = {
      query: 'status',
      mode: 'hybrid',
      denseWeight: 0.5,
      results: [],
    };

    const spy = vi.spyOn(api, 'apiRequest').mockResolvedValue(mockResponse);

    const result = await semanticSearchApi('status');

    expect(spy).toHaveBeenCalledWith('/api/test/semantic-search', {
      method: 'POST',
      body: {
        query: 'status',
        mode: 'hybrid',
        denseWeight: 0.5,
        limit: 15,
      },
    });

    expect(result).toEqual(mockResponse);
  });

  /**
   * @requirement UI-142
   * @category UI
   * @type PositiveFeature
   * @description Returns fallback object when apiRequest returns null or undefined.
   */
  it('returns fallback object when apiRequest returns null or undefined', async () => {
    vi.spyOn(api, 'apiRequest').mockResolvedValue(null as any);

    const result = await semanticSearchApi('test', 'semantic', 0.8, 5);

    expect(result).toEqual({
      query: 'test',
      mode: 'semantic',
      denseWeight: 0.8,
      results: [],
    });
  });
});
