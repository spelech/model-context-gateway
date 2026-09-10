#!/usr/bin/env node
import http from 'http';

const PORT = process.env.PORT || 8090;

// Track active SSE client connections per server endpoint
const sseSubscribers = new Map();

function getSubscribers(endpoint) {
  if (!sseSubscribers.has(endpoint)) {
    sseSubscribers.set(endpoint, []);
  }
  return sseSubscribers.get(endpoint);
}

function parseAuth(req) {
  const authHeader = req.headers['authorization'] || '';
  const xApiKey = req.headers['x-api-key'] || '';
  const xInternalToken = req.headers['x-internal-token'] || '';
  const xForwardedUser = req.headers['x-forwarded-user'] || '';

  let bearerToken = null;
  if (authHeader.startsWith('Bearer ')) {
    bearerToken = authHeader.substring(7).trim();
  }

  return { authHeader, xApiKey, xInternalToken, xForwardedUser, bearerToken };
}

// Validation logic for each downstream server endpoint
function validateEndpointAuth(endpoint, req) {
  const { authHeader, xApiKey, xInternalToken, xForwardedUser, bearerToken } = parseAuth(req);

  switch (endpoint) {
    case 'apikey': {
      // Expects X-API-Key: corp-internal-key-456 or Bearer corp-internal-key-456
      if (xApiKey === 'corp-internal-key-456' || bearerToken === 'corp-internal-key-456') {
        return { valid: true };
      }
      return { valid: false, status: 401, error: 'Unauthorized: Invalid or missing API key. Expected X-API-Key: corp-internal-key-456' };
    }

    case 'customheader': {
      // Expects X-Internal-Token: custom-corp-token-789
      if (xInternalToken === 'custom-corp-token-789') {
        return { valid: true };
      }
      return { valid: false, status: 401, error: 'Unauthorized: Invalid or missing X-Internal-Token header. Expected custom-corp-token-789' };
    }

    case 'slack': {
      // Per-user Slack OAuth token
      const validSlackTokens = {
        'xoxp-steve-slack-token-999': 'steve',
        'xoxp-alice-slack-token-888': 'alice'
      };

      if (!bearerToken || !validSlackTokens[bearerToken]) {
        return { valid: false, status: 401, error: 'Unauthorized: Missing or invalid Slack user OAuth Bearer token.' };
      }

      const tokenOwner = validSlackTokens[bearerToken];
      if (xForwardedUser && xForwardedUser.toLowerCase() !== tokenOwner.toLowerCase()) {
        return {
          valid: false,
          status: 403,
          error: `Forbidden: Slack token belongs to '${tokenOwner}' but X-Forwarded-User is '${xForwardedUser}'.`
        };
      }

      return { valid: true, user: tokenOwner };
    }

    case 'idp-token': {
      // Expects Bearer JWT from In-House IdP with aud: internal-mcp
      if (!bearerToken) {
        return { valid: false, status: 401, error: 'Unauthorized: Missing Bearer JWT from In-House IdP.' };
      }

      try {
        const parts = bearerToken.split('.');
        if (parts.length === 3) {
          let b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
          while (b64.length % 4) b64 += '=';
          const payload = JSON.parse(Buffer.from(b64, 'base64').toString('utf8'));

          if (payload.aud && (payload.aud === 'internal-mcp' || (Array.isArray(payload.aud) && payload.aud.includes('internal-mcp')))) {
            return { valid: true, user: payload.sub || payload.preferred_username };
          }
        }
      } catch (err) {}

      return { valid: false, status: 401, error: 'Unauthorized: Invalid JWT claims or audience. Expected aud=internal-mcp.' };
    }

    case 'forwarded-identity': {
      // Validates presence of X-Forwarded-User
      if (!xForwardedUser) {
        return { valid: false, status: 401, error: 'Unauthorized: Missing X-Forwarded-User identity header.' };
      }
      return { valid: true, user: xForwardedUser };
    }

    default:
      return { valid: true };
  }
}

// Server tools definitions per endpoint
const serverTools = {
  apikey: [
    {
      name: 'internal_lookup_asset',
      description: 'Looks up internal corporate asset by asset ID.',
      inputSchema: { type: 'object', properties: { asset_id: { type: 'string' } }, required: ['asset_id'] }
    },
    {
      name: 'internal_ping',
      description: 'Returns health ping from internal API server.',
      inputSchema: { type: 'object' }
    }
  ],
  customheader: [
    {
      name: 'internal_service_metrics',
      description: 'Returns telemetry metrics for internal microservice.',
      inputSchema: { type: 'object', properties: { metric_name: { type: 'string' } } }
    },
    {
      name: 'internal_service_status',
      description: 'Returns status of internal backend.',
      inputSchema: { type: 'object' }
    }
  ],
  slack: [
    {
      name: 'slack_post_message',
      description: 'Posts a chat message to a Slack channel on behalf of user.',
      inputSchema: {
        type: 'object',
        properties: { channel: { type: 'string' }, text: { type: 'string' } },
        required: ['channel', 'text']
      }
    },
    {
      name: 'slack_list_channels',
      description: 'Lists all public and private channels user has access to.',
      inputSchema: { type: 'object' }
    }
  ],
  'idp-token': [
    {
      name: 'internal_secure_compute',
      description: 'Executes secure compute job verified via internal IdP token.',
      inputSchema: { type: 'object', properties: { job_id: { type: 'string' } }, required: ['job_id'] }
    },
    {
      name: 'internal_ad_directory_query',
      description: 'Queries Active Directory directory metadata.',
      inputSchema: { type: 'object', properties: { query: { type: 'string' } } }
    }
  ],
  'forwarded-identity': [
    {
      name: 'whoami',
      description: 'Returns the caller identity forwarded by the gateway.',
      inputSchema: { type: 'object' }
    }
  ]
};

const server = http.createServer((req, res) => {
  const host = req.headers.host || `localhost:${PORT}`;
  const urlObj = new URL(req.url, `http://${host}`);
  const pathname = urlObj.pathname;

  console.log(`[DOWNSTREAM-MOCK] ${req.method} ${pathname}`);

  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Headers', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  // Determine endpoint category from URL prefix (e.g. /apikey/sse, /slack/message, /idp-token/mcp)
  const segments = pathname.split('/').filter(Boolean);
  const endpoint = segments[0] || 'default';
  const subPath = segments.slice(1).join('/');

  // Health endpoint
  if (endpoint === 'health') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ status: 'healthy', service: 'mock-downstream-mcp-servers' }));
    return;
  }

  // Validate authentication for this endpoint
  const authResult = validateEndpointAuth(endpoint, req);
  if (!authResult.valid) {
    console.log(`[DOWNSTREAM-MOCK] Auth REJECTED on ${endpoint}: ${authResult.error}`);
    res.writeHead(authResult.status, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
      jsonrpc: '2.0',
      error: { code: -32001, message: authResult.error },
      id: null
    }));
    return;
  }

  // 1. SSE Connection Endpoint: /{endpoint}/sse
  if (subPath === 'sse' || pathname === `/${endpoint}/sse`) {
    res.writeHead(200, {
      'Content-Type': 'text/event-stream',
      'Cache-Control': 'no-cache',
      'Connection': 'keep-alive'
    });

    const subs = getSubscribers(endpoint);
    subs.push(res);

    // Send initial endpoint event
    res.write('event: endpoint\n');
    res.write(`data: http://${host}/${endpoint}/message\n\n`);

    const interval = setInterval(() => {
      res.write(': keepalive\n\n');
    }, 15000);

    res.on('close', () => {
      clearInterval(interval);
      const index = subs.indexOf(res);
      if (index !== -1) subs.splice(index, 1);
    });
    return;
  }

  // 2. Message / JSON-RPC Endpoint: /{endpoint}/message or /{endpoint}/mcp or /{endpoint}
  let body = '';
  req.on('data', chunk => { body += chunk; });
  req.on('end', () => {
    let parsedId = 1;
    let method = '';
    let params = {};

    try {
      if (body) {
        const j = JSON.parse(body);
        parsedId = j.id !== undefined ? j.id : 1;
        method = j.method;
        params = j.params || {};
      }
    } catch (e) {}

    console.log(`[DOWNSTREAM-MOCK] [${endpoint}] Method: ${method}, Id: ${parsedId}`);

    const isSsePost = subPath === 'message';
    const sendResponse = (payload) => {
      if (isSsePost) {
        const subs = getSubscribers(endpoint);
        subs.forEach(c => c.write(`event: message\ndata: ${JSON.stringify(payload)}\n\n`));
        res.writeHead(202);
        res.end();
      } else {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify(payload));
      }
    };

    if (method === 'initialize') {
      sendResponse({
        jsonrpc: '2.0',
        id: parsedId,
        result: {
          protocolVersion: '2024-11-05',
          capabilities: { tools: { listChanged: true } },
          serverInfo: { name: `mock-${endpoint}-mcp`, version: '1.0.0' }
        }
      });
      return;
    }

    if (method === 'tools/list') {
      const tools = serverTools[endpoint] || [];
      sendResponse({
        jsonrpc: '2.0',
        id: parsedId,
        result: { tools }
      });
      return;
    }

    if (method === 'tools/call') {
      const toolName = params.name;
      let toolResult = { status: 'success', server: endpoint, executed_at: new Date().toISOString() };

      if (toolName === 'whoami') {
        toolResult.caller = authResult.user || req.headers['x-forwarded-user'] || 'anonymous';
        toolResult.forwarded_headers = {
          'x-forwarded-user': req.headers['x-forwarded-user'] || null
        };
      } else if (toolName === 'slack_post_message') {
        toolResult.posted_by = authResult.user || 'unknown';
        toolResult.channel = params.arguments?.channel;
        toolResult.text = params.arguments?.text;
      }

      sendResponse({
        jsonrpc: '2.0',
        id: parsedId,
        result: {
          content: [{ type: 'text', text: JSON.stringify(toolResult) }],
          isError: false
        }
      });
      return;
    }

    // Default response for notifications or unknown methods
    sendResponse({
      jsonrpc: '2.0',
      id: parsedId,
      result: {}
    });
  });
});

server.listen(PORT, () => {
  console.log(`[DOWNSTREAM-MOCK] Enterprise Downstream MCP Server running on http://0.0.0.0:${PORT}`);
});
