import { useState, useEffect } from 'react';
import { ToolItem, PromptItem, ResourceItem, TemplateItem } from '../../shared/types';
import { fetchTestToolsApi, fetchTestPromptsApi, fetchTestResourcesApi } from '../../api/testbenchApi';

export const useTestBenchState = () => {
  const [activeTab, setActiveTab] = useState<'tools' | 'prompts' | 'resources' | 'semantic'>('tools');

  // Lists
  const [tools, setTools] = useState<ToolItem[]>([]);
  const [prompts, setPrompts] = useState<PromptItem[]>([]);
  const [resourcesData, setResourcesData] = useState<{ resources: ResourceItem[]; templates: TemplateItem[] }>({ resources: [], templates: [] });

  // Tool Selects State
  const [selectedToolServer, setSelectedToolServer] = useState('');
  const [selectedToolName, setSelectedToolName] = useState('');
  const [toolArguments, setToolArguments] = useState<Record<string, any>>({});
  const [rawToolJson, setRawToolJson] = useState('{}');

  // Prompt Selects State
  const [selectedPromptServer, setSelectedPromptServer] = useState('');
  const [selectedPromptName, setSelectedPromptName] = useState('');
  const [promptArguments, setPromptArguments] = useState<Record<string, string>>({});

  // Resource Selects State
  const [selectedResourceServer, setSelectedResourceServer] = useState('');
  const [selectedResourceUri, setSelectedResourceUri] = useState('');
  const [selectedResourceValue, setSelectedResourceValue] = useState('');

  // Semantic
  const [semanticQuery, setSemanticQuery] = useState('');
  const [semanticResults, setSemanticResults] = useState<any[]>([]);
  const [isSearchingSemantic, setIsSearchingSemantic] = useState(false);

  // Console
  const [consoleRequest, setConsoleRequest] = useState('Ready');
  const [consoleResponse, setConsoleResponse] = useState('Waiting for execution...');

  useEffect(() => {
    let ignore = false;
    const fetchInitialData = async () => {
      try {
        const [toolsRes, promptsRes, resourcesRes] = await Promise.allSettled([
          fetchTestToolsApi(),
          fetchTestPromptsApi(),
          fetchTestResourcesApi(),
        ]);
        if (!ignore) {
          if (toolsRes.status === 'fulfilled') {
            setTools(toolsRes.value || []);
          }
          if (promptsRes.status === 'fulfilled') {
            setPrompts(promptsRes.value || []);
          }
          if (resourcesRes.status === 'fulfilled') {
            setResourcesData(resourcesRes.value || { resources: [], templates: [] });
          }
        }
      } catch (e) {
        console.error('Failed to fetch initial test data:', e);
      }
    };

    fetchInitialData();
    return () => {
      ignore = true;
    };
  }, []);

  return {
    activeTab, setActiveTab,
    tools, setTools,
    prompts, setPrompts,
    resourcesData, setResourcesData,
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
  };
};
