import { describe, it, expect } from 'vitest';
import { parseNamespacedName } from '../../shared/utils/mcpNaming';

describe('mcpNaming utility', () => {
  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Correctly parses tool names with slash delimiter
   */
  it('parses tool names with slash delimiter', () => {
    const result = parseNamespacedName('mcp-arr-hd/arr_status');
    expect(result.serverId).toBe('mcp-arr-hd');
    expect(result.cleanName).toBe('arr_status');
    expect(result.isCustom).toBe(false);
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Correctly parses tool names with dunder delimiter
   */
  it('parses tool names with dunder delimiter', () => {
    const result = parseNamespacedName('docker__list_containers');
    expect(result.serverId).toBe('docker');
    expect(result.cleanName).toBe('list_containers');
    expect(result.isCustom).toBe(false);
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Correctly parses tool names with colon delimiter
   */
  it('parses tool names with colon delimiter', () => {
    const result = parseNamespacedName('media:search');
    expect(result.serverId).toBe('media');
    expect(result.cleanName).toBe('search');
    expect(result.isCustom).toBe(false);
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description Handles un-namespaced custom tools
   */
  it('identifies un-namespaced custom tools', () => {
    const result = parseNamespacedName('native_search');
    expect(result.serverId).toBe('custom');
    expect(result.cleanName).toBe('native_search');
    expect(result.isCustom).toBe(true);
  });
});
