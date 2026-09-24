/** @requirement UI-115 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { TestBenchView } from '../../components/testbench/TestBenchView';
import * as testbenchApi from '../../api/testbenchApi';
import * as api from '../../shared/api/api';

describe('TestBenchView Component', () => {
  beforeEach(() => {
    vi.spyOn(testbenchApi, 'fetchTestToolsApi').mockResolvedValue([
      {
        name: 'docker__list_containers',
        description: 'List containers',
        inputSchema: { type: 'object', properties: { all: { type: 'boolean' } } },
      },
    ]);
    vi.spyOn(testbenchApi, 'fetchTestPromptsApi').mockResolvedValue([
      {
        name: 'docker__diagnose',
        description: 'Diagnose container',
        arguments: [{ name: 'container_id', required: true }],
      },
    ]);
    vi.spyOn(testbenchApi, 'fetchTestResourcesApi').mockResolvedValue({
      resources: [
        {
          name: 'Docker Status',
          uri: 'mcp://docker/status',
          description: 'Docker engine status',
        },
      ],
      templates: [
        {
          name: 'Container Log Template',
          uriTemplate: 'mcp://docker/logs/{id}',
        },
      ],
    });
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type Positive
   * @description renders test bench cards and switches tabs
   */
  it('renders test bench cards and switches tabs', async () => {
    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByText('Interactive Tool Tester')).toBeInTheDocument();
    });

    // Switch to Prompts tab
    const promptTab = screen.getByRole('button', { name: /Prompts/i });
    fireEvent.click(promptTab);
    expect(screen.getByText('Interactive Prompt Tester')).toBeInTheDocument();

    // Switch to Resources tab
    const resourceTab = screen.getByRole('button', { name: /Resources/i });
    fireEvent.click(resourceTab);
    expect(screen.getByText('Interactive Resource Tester')).toBeInTheDocument();

    // Switch back to Tools tab
    const toolTab = screen.getByRole('button', { name: /Tools/i });
    fireEvent.click(toolTab);
    expect(screen.getByText('Interactive Tool Tester')).toBeInTheDocument();
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type Positive
   * @description handles semantic search queries in SemanticRouterCard
   */
  it('handles semantic search queries in SemanticRouterCard', async () => {
    vi.spyOn(api, 'apiRequest').mockResolvedValue([
      { name: 'docker__list_containers', score: 0.95, description: 'List containers' },
    ]);

    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/e\.g\. search matrix in plex/i)).toBeInTheDocument();
    });

    const searchInput = screen.getByPlaceholderText(/e\.g\. search matrix in plex/i);
    fireEvent.change(searchInput, { target: { value: 'list all docker containers' } });

    const searchBtn = screen.getByRole('button', { name: /Test Filter Score/i });
    fireEvent.click(searchBtn);

    await waitFor(() => {
      expect(api.apiRequest).toHaveBeenCalledWith('/api/test/semantic-search', expect.anything());
    });
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type Positive
   * @description executes tool and updates console
   */
  it('executes tool and updates console', async () => {
    vi.spyOn(api, 'apiRequest').mockResolvedValue({
      content: [{ type: 'text', text: '{"status":"ok"}' }],
    });

    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    const serverSelect = screen.getByLabelText('Server');
    fireEvent.change(serverSelect, { target: { value: 'docker' } });

    const toolSelect = screen.getByLabelText('Tool');
    fireEvent.change(toolSelect, { target: { value: 'docker__list_containers' } });

    const forms = document.querySelectorAll('form');
    fireEvent.submit(forms[0]);

    await waitFor(() => {
      expect(api.apiRequest).toHaveBeenCalledWith('/api/test/call', expect.anything());
    });
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type Positive
   * @description executes prompt get in prompt tester tab
   */
  it('executes prompt get in prompt tester tab', async () => {
    vi.spyOn(api, 'apiRequest').mockResolvedValue({
      messages: [{ role: 'user', content: { type: 'text', text: 'Diagnose stopped container' } }],
    });

    render(<TestBenchView />);

    const promptTab = screen.getByRole('button', { name: /Prompts/i });
    fireEvent.click(promptTab);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    const serverSelect = screen.getByLabelText('Server');
    fireEvent.change(serverSelect, { target: { value: 'docker' } });

    const promptSelect = screen.getByLabelText('Prompt');
    fireEvent.change(promptSelect, { target: { value: 'docker__diagnose' } });

    const forms = document.querySelectorAll('form');
    fireEvent.submit(forms[0]);

    await waitFor(() => {
      expect(api.apiRequest).toHaveBeenCalledWith('/api/test/prompts/get', expect.anything());
    });
  });

  /**
   * @requirement UI-01
   * @category UI
   * @type Positive
   * @description executes resource read in resource inspector tab
   */
  it('executes resource read in resource inspector tab', async () => {
    vi.spyOn(api, 'apiRequest').mockResolvedValue({
      contents: [{ uri: 'mcp://docker/status', text: 'running' }],
    });

    render(<TestBenchView />);

    const resourceTab = screen.getByRole('button', { name: /Resources/i });
    fireEvent.click(resourceTab);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    const serverSelect = screen.getByLabelText('Server');
    fireEvent.change(serverSelect, { target: { value: 'docker' } });

    const uriInput = screen.getByLabelText('Resource URI');
    fireEvent.change(uriInput, { target: { value: 'mcp://docker/status' } });

    const forms = document.querySelectorAll('form');
    fireEvent.submit(forms[0]);

    await waitFor(() => {
      expect(api.apiRequest).toHaveBeenCalledWith('/api/test/resources/read', expect.anything());
    });
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description groups slash-namespaced tools into distinct backend servers without grouping under custom
   */
  it('groups slash-namespaced tools into distinct backend servers without grouping under custom', async () => {
    vi.spyOn(testbenchApi, 'fetchTestToolsApi').mockResolvedValue([
      {
        name: 'mcp-arr-hd/arr_status',
        description: 'Get Radarr/Sonarr queue status',
        inputSchema: { type: 'object', properties: {} },
      },
      {
        name: 'docker__list_containers',
        description: 'List containers',
        inputSchema: { type: 'object', properties: { all: { type: 'boolean' } } },
      },
    ]);

    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    const serverSelect = screen.getByLabelText('Server') as HTMLSelectElement;
    const options = Array.from(serverSelect.options).map((opt) => opt.text);

    expect(options).toContain('MCP-ARR-HD Server');
    expect(options).toContain('DOCKER Server');
    expect(options).not.toContain('Native C# Registry (custom)');
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description filters tools by selected server and displays clean tool names
   */
  it('filters tools by selected server and displays clean tool names', async () => {
    vi.spyOn(testbenchApi, 'fetchTestToolsApi').mockResolvedValue([
      {
        name: 'mcp-arr-hd/arr_status',
        description: 'Get Radarr/Sonarr queue status',
        inputSchema: { type: 'object', properties: {} },
      },
      {
        name: 'docker__list_containers',
        description: 'List containers',
        inputSchema: { type: 'object', properties: { all: { type: 'boolean' } } },
      },
    ]);

    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    const serverSelect = screen.getByLabelText('Server');
    fireEvent.change(serverSelect, { target: { value: 'mcp-arr-hd' } });

    const toolSelect = screen.getByLabelText('Tool') as HTMLSelectElement;
    const toolOptions = Array.from(toolSelect.options).map((opt) => opt.text);

    expect(toolOptions).toContain('arr_status');
    expect(toolOptions).not.toContain('mcp-arr-hd/arr_status');
    expect(toolOptions).not.toContain('list_containers');
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description displays tool hint banner with clean name, server badge, full description, and parameter badge when tool is selected
   */
  it('displays tool hint banner with clean name, server badge, full description, and parameter badge when tool is selected', async () => {
    vi.spyOn(testbenchApi, 'fetchTestToolsApi').mockResolvedValue([
      {
        name: 'mcp-arr-hd/arr_status',
        description: 'Get Radarr/Sonarr queue status',
        inputSchema: { type: 'object', properties: {} },
      },
    ]);

    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    const serverSelect = screen.getByLabelText('Server');
    fireEvent.change(serverSelect, { target: { value: 'mcp-arr-hd' } });

    const toolSelect = screen.getByLabelText('Tool');
    fireEvent.change(toolSelect, { target: { value: 'mcp-arr-hd/arr_status' } });

    await waitFor(() => {
      expect(screen.getByText('Get Radarr/Sonarr queue status')).toBeInTheDocument();
    });

    expect(screen.getByText('[MCP-ARR-HD]')).toBeInTheDocument();
    expect(screen.getByText(/ready to execute|no arguments/i)).toBeInTheDocument();
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description renders enum parameters as select dropdown with placeholder and options
   */
  it('renders enum parameters as select dropdown with placeholder and options', async () => {
    vi.spyOn(testbenchApi, 'fetchTestToolsApi').mockResolvedValue([
      {
        name: 'media/filter',
        description: 'Filter media items',
        inputSchema: {
          type: 'object',
          properties: {
            format: {
              type: 'string',
              enum: ['json', 'csv', 'xml'],
              description: 'Output format',
            },
          },
          required: ['format'],
        },
      },
    ]);

    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText('Server'), { target: { value: 'media' } });
    fireEvent.change(screen.getByLabelText('Tool'), { target: { value: 'media/filter' } });

    await waitFor(() => {
      expect(screen.getByText('-- Select format --')).toBeInTheDocument();
    });

    const enumSelect = screen.getByRole('combobox', { name: /format/i });
    expect(enumSelect).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'json' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'csv' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'xml' })).toBeInTheDocument();
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description populates argument values when Pre-fill Example button is clicked
   */
  it('populates argument values when Pre-fill Example button is clicked', async () => {
    vi.spyOn(testbenchApi, 'fetchTestToolsApi').mockResolvedValue([
      {
        name: 'docker__run',
        description: 'Run container',
        inputSchema: {
          type: 'object',
          properties: {
            image: { type: 'string', default: 'alpine:latest' },
            instances: { type: 'integer', default: 3 },
            mode: { type: 'string', enum: ['host', 'bridge'] },
          },
          required: ['image'],
        },
      },
    ]);

    render(<TestBenchView />);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    fireEvent.change(screen.getByLabelText('Server'), { target: { value: 'docker' } });
    fireEvent.change(screen.getByLabelText('Tool'), { target: { value: 'docker__run' } });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Pre-fill Example/i })).toBeInTheDocument();
    });

    const prefillBtn = screen.getByRole('button', { name: /Pre-fill Example/i });
    fireEvent.click(prefillBtn);

    const imageInput = screen.getByRole('textbox', { name: /image/i }) as HTMLInputElement;
    expect(imageInput.value).toBe('alpine:latest');

    const instancesInput = screen.getByRole('spinbutton', { name: /instances/i }) as HTMLInputElement;
    expect(instancesInput.value).toBe('3');

    const modeSelect = screen.getByRole('combobox', { name: /mode/i }) as HTMLSelectElement;
    expect(modeSelect.value).toBe('host');

    vi.spyOn(api, 'apiRequest').mockResolvedValue({
      content: [{ type: 'text', text: '{"status":"container started"}' }],
    });

    const forms = document.querySelectorAll('form');
    fireEvent.submit(forms[0]);

    await waitFor(() => {
      expect(api.apiRequest).toHaveBeenCalledWith('/api/test/call', {
        method: 'POST',
        body: {
          serverId: 'docker',
          toolName: 'docker__run',
          name: 'docker__run',
          arguments: {
            image: 'alpine:latest',
            instances: 3,
            mode: 'host',
          },
        },
      });
    });
  });

  /**
   * @requirement UI-132
   * @category UI
   * @type PositiveFeature
   * @description groups slash and dunder namespaced prompts into servers and displays prompt hint banner
   */
  it('groups slash and dunder namespaced prompts into servers and displays prompt hint banner', async () => {
    vi.spyOn(testbenchApi, 'fetchTestPromptsApi').mockResolvedValue([
      {
        name: 'mcp-arr-hd/diagnose_queue',
        description: 'Diagnose Sonarr queue status',
        arguments: [{ name: 'queue_id', required: true, description: 'ID of queue' }],
      },
      {
        name: 'router__summarize',
        description: 'Summarize system state',
      },
    ]);

    render(<TestBenchView />);

    const promptTab = screen.getByRole('button', { name: /Prompts/i });
    fireEvent.click(promptTab);

    await waitFor(() => {
      expect(screen.getByLabelText('Server')).toBeInTheDocument();
    });

    const serverSelect = screen.getByLabelText('Server') as HTMLSelectElement;
    const options = Array.from(serverSelect.options).map((opt) => opt.text);

    expect(options).toContain('MCP-ARR-HD Server');
    expect(options).toContain('Built-in Meta Workflows (router)');

    fireEvent.change(serverSelect, { target: { value: 'mcp-arr-hd' } });

    const promptSelect = screen.getByLabelText('Prompt') as HTMLSelectElement;
    const promptOptions = Array.from(promptSelect.options).map((opt) => opt.text);

    expect(promptOptions).toContain('diagnose_queue');
    expect(promptOptions).not.toContain('mcp-arr-hd/diagnose_queue');

    fireEvent.change(promptSelect, { target: { value: 'mcp-arr-hd/diagnose_queue' } });

    await waitFor(() => {
      expect(screen.getByText('Diagnose Sonarr queue status')).toBeInTheDocument();
    });

    expect(screen.getByText('[MCP-ARR-HD]')).toBeInTheDocument();
    expect(screen.getByText(/1 required argument/i)).toBeInTheDocument();
  });
});
