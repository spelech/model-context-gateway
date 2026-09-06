export interface McpServer {
  id: string;
  alias?: string;
  displayName: string;
  url: string;
  enabled: boolean;
  hidden: boolean;
  type: string;
  categories: string[];
  secretProvider?: string;
  secretItemKey?: string;
  authShape?: string;
  customHeaderName?: string;
  headersJson?: string;
  hasApiKey: boolean;
  apiKey?: string;
  connectionStatus: string;
  connectionAttempts: number;
  connectionError: string;
  allowPassThroughAuth: boolean;
  dynamicAuthPrompt?: string;
}

export interface ServerPayload {
  id?: string;
  alias?: string;
  displayName: string;
  type: string;
  categories: string[];
  url: string;
  secretProvider: string;
  secretItemKey: string;
  authShape: string;
  customHeaderName: string;
  apiKey?: string;
  enabled: boolean;
  hidden: boolean;
  allowPassThroughAuth: boolean;
  dynamicAuthPrompt?: string;
}

export interface InspectCapabilityData {
  tools: any[];
  resources: any[];
  prompts: any[];
}
