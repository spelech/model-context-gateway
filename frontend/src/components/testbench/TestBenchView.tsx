import React from 'react';
import { executeToolApi, getPromptApi, readResourceApi, semanticSearchApi } from '../../api/testbenchApi';
import { showToast } from '../../stores/useToastStore';
import { useTestBenchState } from './useTestBenchState';

import { ToolTesterCard } from './ToolTesterCard';
import { PromptTesterCard } from './PromptTesterCard';
import { ResourceTesterCard } from './ResourceTesterCard';
import { SemanticRouterCard } from './SemanticRouterCard';
import { ConsoleCard } from './ConsoleCard';
import { LogsTerminalCard } from './LogsTerminalCard';

export const TestBenchView: React.FC = () => {
  const {
    activeTab, setActiveTab,
    tools,
    prompts,
    resourcesData,
    selectedToolServer, setSelectedToolServer,
    selectedToolName, setSelectedToolName,
    toolArguments, setToolArguments,
    rawToolJson, setRawToolJson,
    selectedPromptServer, setSelectedPromptServer,
    selectedPromptName, setSelectedPromptName,
    promptArguments, setPromptArguments,
    selectedResourceServer, setSelectedResourceServer,
    selectedResourceUri, setSelectedResourceUri,
    selectedResourceValue, setSelectedResourceValue,
    semanticQuery, setSemanticQuery,
    semanticResults, setSemanticResults,
    isSearchingSemantic, setIsSearchingSemantic,
    consoleRequest, setConsoleRequest,
    consoleResponse, setConsoleResponse
  } = useTestBenchState();

  // Run Tools Call
  const handleToolServerChange = (server: string) => {
    setSelectedToolServer(server);
    setSelectedToolName('');
    setToolArguments({});
    setRawToolJson('{}');
  };

  const handleToolNameChange = (name: string) => {
    setSelectedToolName(name);
    setToolArguments({});
    const tool = tools.find((t) => t.name === name);
    if (tool && tool.inputSchema && tool.inputSchema.properties) {
      const initial: Record<string, any> = {};
      Object.entries(tool.inputSchema.properties).forEach(([key, prop]: [string, any]) => {
        if (prop.type === 'boolean') {
          initial[key] = false;
        }
      });
      setToolArguments(initial);
      setRawToolJson(JSON.stringify(initial, null, 2));
    } else {
      setRawToolJson('{}');
    }
  };

  const handleArgInputChange = (key: string, type: string, value: any) => {
    let finalValue = value;
    if (type === 'boolean') {
      finalValue = !!value;
    } else if (type === 'number') {
      finalValue = value === '' ? '' : Number(value);
    } else if (type === 'array' || type === 'object') {
      try {
        finalValue = JSON.parse(value);
      } catch {
        finalValue = value;
      }
    }

    const updated = { ...toolArguments, [key]: finalValue };
    setToolArguments(updated);
    setRawToolJson(JSON.stringify(updated, null, 2));
  };

  const handleBulkArgsChange = (args: Record<string, any>) => {
    setToolArguments(args);
    setRawToolJson(JSON.stringify(args, null, 2));
  };

  const runToolCall = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedToolName) {
      showToast('Please select a tool to execute', 'error');
      return;
    }

    let parsedArgs = toolArguments;
    try {
      if (rawToolJson && rawToolJson.trim() !== '') {
        parsedArgs = JSON.parse(rawToolJson);
      }
    } catch {
      showToast('Invalid raw arguments JSON', 'error');
      return;
    }

    const rpcPayload = {
      jsonrpc: '2.0',
      id: Math.floor(Math.random() * 1000000),
      method: 'tools/call',
      params: {
        name: selectedToolName,
        arguments: parsedArgs
      }
    };

    setConsoleRequest(JSON.stringify(rpcPayload, null, 2));
    setConsoleResponse('Executing tool...');

    try {
      const result = await executeToolApi(selectedToolServer, selectedToolName, parsedArgs);
      setConsoleResponse(JSON.stringify(result, null, 2));
    } catch (err: any) {
      setConsoleResponse(`Error: ${err.message}`);
    }
  };

  // Prompts handlers
  const handlePromptServerChange = (server: string) => {
    setSelectedPromptServer(server);
    setSelectedPromptName('');
    setPromptArguments({});
  };

  const handlePromptNameChange = (name: string) => {
    setSelectedPromptName(name);
    setPromptArguments({});
  };

  const handlePromptArgChange = (name: string, value: string) => {
    setPromptArguments({ ...promptArguments, [name]: value });
  };

  const runPromptGet = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedPromptName) {
      showToast('Please select a prompt template', 'error');
      return;
    }

    const rpcPayload = {
      jsonrpc: '2.0',
      id: Math.floor(Math.random() * 1000000),
      method: 'prompts/get',
      params: {
        name: selectedPromptName,
        arguments: promptArguments
      }
    };

    setConsoleRequest(JSON.stringify(rpcPayload, null, 2));
    setConsoleResponse('Fetching prompt...');

    try {
      const result = await getPromptApi(selectedPromptServer, selectedPromptName, promptArguments);
      setConsoleResponse(JSON.stringify(result, null, 2));
    } catch (err: any) {
      setConsoleResponse(`Error: ${err.message}`);
    }
  };

  // Resources handlers
  const handleResourceServerChange = (server: string) => {
    setSelectedResourceServer(server);
    setSelectedResourceUri('');
    setSelectedResourceValue('');
  };

  const handleResourceSelectChange = (val: string) => {
    setSelectedResourceValue(val);
    setSelectedResourceUri(val);
  };

  const runResourceRead = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedResourceUri) {
      showToast('Please enter or select a resource URI', 'error');
      return;
    }

    const rpcPayload = {
      jsonrpc: '2.0',
      id: Math.floor(Math.random() * 1000000),
      method: 'resources/read',
      params: {
        uri: selectedResourceUri
      }
    };

    setConsoleRequest(JSON.stringify(rpcPayload, null, 2));
    setConsoleResponse('Reading resource...');

    try {
      const result = await readResourceApi(selectedResourceServer, selectedResourceUri);
      setConsoleResponse(JSON.stringify(result, null, 2));
    } catch (err: any) {
      setConsoleResponse(`Error: ${err.message}`);
    }
  };

  // Semantic
  const runSemanticSearch = async (e?: React.FormEvent) => {
    if (e && e.preventDefault) e.preventDefault();
    if (!semanticQuery.trim()) return;

    setIsSearchingSemantic(true);
    try {
      const results = await semanticSearchApi(semanticQuery.trim());
      setSemanticResults(results || []);
    } catch (err: any) {
      showToast(`Semantic search failed: ${err.message}`, 'error');
      setSemanticResults([]);
    } finally {
      setIsSearchingSemantic(false);
    }
  };

  return (
    <div id="view-testbench" className="view-panel active">
      {/* Top Tab Switcher */}
      <div className="tester-tabs">
        <button
          type="button"
          className={`tester-tab-btn ${activeTab === 'tools' ? 'active' : ''}`}
          onClick={() => setActiveTab('tools')}
        >
          <i className="fa-solid fa-wrench"></i> Tools
        </button>
        <button
          type="button"
          className={`tester-tab-btn ${activeTab === 'prompts' ? 'active' : ''}`}
          onClick={() => setActiveTab('prompts')}
        >
          <i className="fa-solid fa-comments"></i> Prompts
        </button>
        <button
          type="button"
          className={`tester-tab-btn ${activeTab === 'resources' ? 'active' : ''}`}
          onClick={() => setActiveTab('resources')}
        >
          <i className="fa-solid fa-file-invoice"></i> Resources
        </button>
      </div>

      <div className="tester-container">
        {/* Left Column: Form execution */}
        <div className="tester-panel">
          {activeTab === 'tools' && (
            <ToolTesterCard
              tools={tools}
              selectedServer={selectedToolServer}
              selectedToolName={selectedToolName}
              toolArguments={toolArguments}
              rawToolJson={rawToolJson}
              onServerChange={handleToolServerChange}
              onToolChange={handleToolNameChange}
              onArgChange={handleArgInputChange}
              onBulkArgsChange={handleBulkArgsChange}
              onRawJsonChange={setRawToolJson}
              onSubmit={runToolCall}
            />
          )}

          {activeTab === 'prompts' && (
            <PromptTesterCard
              prompts={prompts}
              selectedServer={selectedPromptServer}
              selectedPromptName={selectedPromptName}
              promptArguments={promptArguments}
              onServerChange={handlePromptServerChange}
              onPromptChange={handlePromptNameChange}
              onArgChange={handlePromptArgChange}
              onSubmit={runPromptGet}
            />
          )}

          {activeTab === 'resources' && (
            <ResourceTesterCard
              resourcesData={resourcesData}
              selectedServer={selectedResourceServer}
              selectedResourceUri={selectedResourceUri}
              selectedResourceValue={selectedResourceValue}
              onServerChange={handleResourceServerChange}
              onSelectChange={handleResourceSelectChange}
              onUriChange={setSelectedResourceUri}
              onSubmit={runResourceRead}
            />
          )}
        </div>

        {/* Right Column: Console & Semantic */}
        <div className="tester-panel">
          <SemanticRouterCard
            semanticQuery={semanticQuery}
            semanticResults={semanticResults}
            isSearchingSemantic={isSearchingSemantic}
            onQueryChange={setSemanticQuery}
            onSearch={runSemanticSearch}
          />

          <ConsoleCard consoleRequest={consoleRequest} consoleResponse={consoleResponse} />
        </div>

        {/* Bottom Full Width System logs */}
        <LogsTerminalCard />
      </div>
    </div>
  );
};
export default TestBenchView;
