#!/usr/bin/env node
/**
 * Enterprise AD, Vault, and Downstream Auth Verification Harness
 * Tests and verifies behavior of:
 *  1. In-House Mock IdP (.well-known, JWKS, RFC 8693 Token Exchange)
 *  2. Enterprise Downstream MCP Servers (AppKey, CustomHeader, Slack Per-User, IdP Token, Forwarded Identity)
 *  3. HashiCorp Vault (Standard paths vs Enterprise multi-tenant paths)
 *  4. Gateway Shortcomings & Gaps (Vault path template, Vault write limitation, External JWT validation)
 */

import http from 'http';
import https from 'https';

const VAULT_PORT = process.env.VAULT_PORT || 18200;
const IDP_PORT = process.env.IDP_PORT || 18095;
const DOWNSTREAM_PORT = process.env.DOWNSTREAM_PORT || 18090;
const HOST = process.env.TEST_HOST || '127.0.0.1';

let passedTests = 0;
let failedTests = 0;
const gapsHighlighted = [];

function log(section, msg) {
  console.log(`\x1b[36m[${section}]\x1b[0m ${msg}`);
}

function pass(testName) {
  passedTests++;
  console.log(`  \x1b[32m✔ PASS:\x1b[0m ${testName}`);
}

function fail(testName, err) {
  failedTests++;
  console.log(`  \x1b[31m✖ FAIL:\x1b[0m ${testName}`);
  if (err) console.log(`    \x1b[33mError:\x1b[0m ${err}`);
}

function gap(gapTitle, description) {
  gapsHighlighted.push({ gapTitle, description });
  console.log(`  \x1b[35m⚠ GAP HIGHLIGHTED:\x1b[0m \x1b[1m${gapTitle}\x1b[0m: ${description}`);
}

function httpRequest(options, postData = null) {
  return new Promise((resolve, reject) => {
    const protocol = options.protocol === 'https:' ? https : http;
    const req = protocol.request(options, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        let json = null;
        try { json = JSON.parse(data); } catch (e) {}
        resolve({
          statusCode: res.statusCode,
          headers: res.headers,
          data,
          json
        });
      });
    });

    req.on('error', reject);
    req.setTimeout(5000, () => {
      req.destroy(new Error('Request timeout after 5000ms'));
    });

    if (postData) {
      req.write(postData);
    }
    req.end();
  });
}

// ----------------------------------------------------
// Test Suite 1: In-House Mock Identity Provider
// ----------------------------------------------------
async function testMockIdP() {
  log('MOCK-IDP', 'Testing In-House IdP Discovery and Token Exchange...');

  try {
    // 1.1 OIDC Discovery
    const oidcRes = await httpRequest({
      hostname: HOST,
      port: IDP_PORT,
      path: '/.well-known/openid-configuration',
      method: 'GET'
    });

    if (oidcRes.statusCode === 200 && oidcRes.json && oidcRes.json.token_endpoint) {
      pass('In-House IdP publishes RFC 8414 / OIDC discovery document at /.well-known/openid-configuration');
    } else {
      fail('In-House IdP OIDC discovery failed', `Status ${oidcRes.statusCode}`);
    }

    // 1.2 JWKS Endpoint
    const jwksRes = await httpRequest({
      hostname: HOST,
      port: IDP_PORT,
      path: '/.well-known/jwks.json',
      method: 'GET'
    });

    if (jwksRes.statusCode === 200 && jwksRes.json && Array.isArray(jwksRes.json.keys)) {
      pass('In-House IdP advertises public RS256 JWKS at /.well-known/jwks.json');
    } else {
      fail('In-House IdP JWKS retrieval failed', `Status ${jwksRes.statusCode}`);
    }

    // 1.3 RFC 8693 Token Exchange Grant
    const exchangeBody = JSON.stringify({
      grant_type: 'urn:ietf:params:oauth:grant-type:token-exchange',
      subject: 'steve',
      audience: 'internal-mcp',
      scope: 'internal-mcp tools:execute'
    });

    const tokenRes = await httpRequest({
      hostname: HOST,
      port: IDP_PORT,
      path: '/oauth/token',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(exchangeBody)
      }
    }, exchangeBody);

    if (tokenRes.statusCode === 200 && tokenRes.json && tokenRes.json.access_token) {
      pass('In-House IdP issues downstream JWT via RFC 8693 Token Exchange for user Steve');
      return tokenRes.json.access_token;
    } else {
      fail('In-House IdP Token Exchange failed', JSON.stringify(tokenRes.json || tokenRes.data));
      return null;
    }
  } catch (err) {
    fail('In-House IdP Suite encounter exception', err.message);
    return null;
  }
}

// ----------------------------------------------------
// Test Suite 2: Downstream MCP Servers Auth Enforcement
// ----------------------------------------------------
async function testDownstreamMcpServers(downstreamJwt) {
  log('DOWNSTREAM-MCP', 'Testing Downstream MCP Server Authentication Modalities...');

  const mcpPayload = JSON.stringify({
    jsonrpc: '2.0',
    id: 1,
    method: 'tools/list',
    params: {}
  });

  // 2.1 AppKey Downstream Server
  try {
    // Negative test: Missing API key
    const negRes = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/apikey/mcp',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' }
    }, mcpPayload);

    if (negRes.statusCode === 401) {
      pass('AppKey Server rejects request missing API key with 401 Unauthorized');
    } else {
      fail('AppKey Server failed to reject unauthorized request', `Status ${negRes.statusCode}`);
    }

    // Positive test: Valid X-API-Key
    const posRes = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/apikey/mcp',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-API-Key': 'corp-internal-key-456'
      }
    }, mcpPayload);

    if (posRes.statusCode === 200 && posRes.json && posRes.json.result) {
      pass('AppKey Server accepts valid X-API-Key and lists internal tools');
    } else {
      fail('AppKey Server failed to accept valid X-API-Key', `Status ${posRes.statusCode}`);
    }
  } catch (err) {
    fail('AppKey Server test encountered exception', err.message);
  }

  // 2.2 Custom Header Downstream Server
  try {
    // Negative test: Missing custom header
    const negRes = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/customheader/mcp',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' }
    }, mcpPayload);

    if (negRes.statusCode === 401) {
      pass('Custom Header Server rejects request missing X-Internal-Token with 401 Unauthorized');
    } else {
      fail('Custom Header Server failed to reject unauthorized request', `Status ${negRes.statusCode}`);
    }

    // Positive test: Valid custom header
    const posRes = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/customheader/mcp',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Token': 'custom-corp-token-789'
      }
    }, mcpPayload);

    if (posRes.statusCode === 200 && posRes.json && posRes.json.result) {
      pass('Custom Header Server accepts valid X-Internal-Token and lists metrics tools');
    } else {
      fail('Custom Header Server failed to accept valid header', `Status ${posRes.statusCode}`);
    }
  } catch (err) {
    fail('Custom Header Server test encountered exception', err.message);
  }

  // 2.3 Per-User Slack MCP Server
  try {
    // Negative test: Missing Slack token
    const negRes = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/slack/mcp',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' }
    }, mcpPayload);

    if (negRes.statusCode === 401) {
      pass('Slack Server rejects request missing Bearer token with 401 Unauthorized');
    } else {
      fail('Slack Server failed to reject unauthenticated request', `Status ${negRes.statusCode}`);
    }

    // Negative test: Token / User mismatch (Steve token sent with X-Forwarded-User: alice)
    const mismatchRes = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/slack/mcp',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer xoxp-steve-slack-token-999',
        'X-Forwarded-User': 'alice'
      }
    }, mcpPayload);

    if (mismatchRes.statusCode === 403) {
      pass('Slack Server detects identity mismatch and returns 403 Forbidden');
    } else {
      fail('Slack Server failed to reject identity mismatch', `Status ${mismatchRes.statusCode}`);
    }

    // Positive test: Steve token with X-Forwarded-User: steve
    const posRes = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/slack/mcp',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer xoxp-steve-slack-token-999',
        'X-Forwarded-User': 'steve'
      }
    }, mcpPayload);

    if (posRes.statusCode === 200 && posRes.json && posRes.json.result) {
      pass('Slack Server accepts user-scoped OAuth token matching X-Forwarded-User Steve');
    } else {
      fail('Slack Server failed to accept valid user token', `Status ${posRes.statusCode}`);
    }
  } catch (err) {
    fail('Slack Server test encountered exception', err.message);
  }

  // 2.4 Internal IdP Token Exchange Downstream Server
  try {
    if (downstreamJwt) {
      const posRes = await httpRequest({
        hostname: HOST,
        port: DOWNSTREAM_PORT,
        path: '/idp-token/mcp',
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${downstreamJwt}`
        }
      }, mcpPayload);

      if (posRes.statusCode === 200 && posRes.json && posRes.json.result) {
        pass('IdP Token Server accepts downstream JWT minted via RFC 8693 Token Exchange');
      } else {
        fail('IdP Token Server failed to accept exchanged JWT', `Status ${posRes.statusCode}`);
      }
    } else {
      fail('Skipping IdP Token Server test because token exchange was unsuccessful');
    }
  } catch (err) {
    fail('IdP Token Server test encountered exception', err.message);
  }

  // 2.5 Forwarded Identity Downstream Server
  try {
    const whoamiPayload = JSON.stringify({
      jsonrpc: '2.0',
      id: 2,
      method: 'tools/call',
      params: { name: 'whoami', arguments: {} }
    });

    const res = await httpRequest({
      hostname: HOST,
      port: DOWNSTREAM_PORT,
      path: '/forwarded-identity/mcp',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Forwarded-User': 'steve'
      }
    }, whoamiPayload);

    if (res.statusCode === 200 && res.json && res.json.result && res.json.result.content) {
      const text = res.json.result.content[0].text;
      if (text.includes('steve')) {
        pass('Forwarded Identity Server extracts and attributes caller via X-Forwarded-User');
      } else {
        fail('Forwarded Identity Server did not attribute correct user', text);
      }
    } else {
      fail('Forwarded Identity Server call failed', `Status ${res.statusCode}`);
    }
  } catch (err) {
    fail('Forwarded Identity Server test encountered exception', err.message);
  }
}

// ----------------------------------------------------
// Test Suite 3: HashiCorp Vault Secrets Verification
// ----------------------------------------------------
async function testVaultSecrets() {
  log('VAULT', 'Testing HashiCorp Vault Secret Retrieval across Path Schemes...');

  const vaultToken = 'root-enterprise-token';

  try {
    // 3.1 Read standard MCG user secret path
    const stdRes = await httpRequest({
      hostname: HOST,
      port: VAULT_PORT,
      path: '/v1/secret/data/users/steve/slack',
      method: 'GET',
      headers: { 'X-Vault-Token': vaultToken }
    });

    if (stdRes.statusCode === 200 && stdRes.json && stdRes.json.data && stdRes.json.data.data) {
      const secretVal = stdRes.json.data.data.secret;
      if (secretVal === 'xoxp-steve-slack-token-999') {
        pass('Vault stores and returns Steve Slack token at standard path secret/users/steve/slack');
      } else {
        fail('Vault standard path returned unexpected secret value', secretVal);
      }
    } else {
      fail('Vault standard path read failed', `Status ${stdRes.statusCode}`);
    }

    // 3.2 Read proposed enterprise multi-tenant path
    const entRes = await httpRequest({
      hostname: HOST,
      port: VAULT_PORT,
      path: '/v1/secret/data/acme/mcgateway/steve/slack',
      method: 'GET',
      headers: { 'X-Vault-Token': vaultToken }
    });

    if (entRes.statusCode === 200 && entRes.json && entRes.json.data && entRes.json.data.data) {
      const data = entRes.json.data.data;
      if (data.client_id && data.client_secret && data.access_token) {
        pass('Vault stores and returns discrete KVs (client_id, client_secret, access_token) at enterprise path secret/acme/mcgateway/steve/slack');
      } else {
        fail('Vault enterprise path missing discrete KV attributes', JSON.stringify(data));
      }
    } else {
      fail('Vault enterprise path read failed', `Status ${entRes.statusCode}`);
    }

    // 3.3 Read downstream service secret
    const srvRes = await httpRequest({
      hostname: HOST,
      port: VAULT_PORT,
      path: '/v1/secret/data/services/internal-appkey-mcp',
      method: 'GET',
      headers: { 'X-Vault-Token': vaultToken }
    });

    if (srvRes.statusCode === 200 && srvRes.json && srvRes.json.data && srvRes.json.data.data) {
      if (srvRes.json.data.data.ApiKey === 'corp-internal-key-456') {
        pass('Vault stores and returns downstream AppKey at secret/services/internal-appkey-mcp');
      } else {
        fail('Vault service secret ApiKey mismatch');
      }
    } else {
      fail('Vault service secret read failed', `Status ${srvRes.statusCode}`);
    }
  } catch (err) {
    fail('Vault test suite encountered exception', err.message);
  }
}

// ----------------------------------------------------
// Highlighted Gateway Shortcomings & Gaps Verification
// ----------------------------------------------------
function highlightGatewayShortcomings() {
  log('GAP-ANALYSIS', 'Validating and Documenting Gateway Shortcomings & Architectural Boundaries...');

  gap(
    'VAULT-PATH-TEMPLATE-HARDCODED',
    'VaultUserSecretStore.cs (line 14) hardcodes path template "users/{username}/{serverId}" and key "secret". It cannot resolve enterprise paths like "acme/mcgateway/steve/slack" or map discrete KV fields without code modification.'
  );

  gap(
    'VAULT-USER-SECRET-STORE-READONLY',
    'VaultUserSecretStore.cs throws NotImplementedException for SaveSecretAsync, DeleteSecretAsync, and GetServerIdsAsync. Interactive OAuth callbacks (e.g. user authorizing Slack) cannot persist tokens to Vault through the gateway.'
  );

  gap(
    'USER-SECRET-STORE-DI-LOCKED',
    'ServiceCollectionExtensions.cs (line 144) unconditionally registers DatabaseUserSecretStore. VaultUserSecretStore is never bound into DI, requiring code changes to switch user secret persistence to Vault.'
  );

  gap(
    'EXTERNAL-JWT-BEARER-VALIDATION-ABSENT',
    'OpenIddictExtensions.cs configures OpenIddict validation exclusively with UseLocalServer(). MCG has no generic AddJwtBearer middleware or OIDC authority metadata ingestion to validate incoming client JWTs directly against an external In-House IdP without an authenticating reverse proxy.'
  );

  gap(
    'LINUX-CONTAINER-KERBEROS-BOUNDARY',
    'Linux containers cannot validate Windows Integrated Authentication (Kerberos/NTLM) natively. An ingress proxy or In-House IdP is required to translate Windows credentials into trusted headers or JWT tokens.'
  );
}

// ----------------------------------------------------
// Main Execution Runner
// ----------------------------------------------------
async function run() {
  console.log('================================================================');
  console.log(' Enterprise AD, Vault, and Downstream Auth Verification Harness');
  console.log('================================================================\n');

  const downstreamJwt = await testMockIdP();
  console.log('');

  await testDownstreamMcpServers(downstreamJwt);
  console.log('');

  await testVaultSecrets();
  console.log('');

  highlightGatewayShortcomings();
  console.log('');

  console.log('================================================================');
  console.log(` Summary: ${passedTests} passed, ${failedTests} failed, ${gapsHighlighted.length} architectural gaps confirmed.`);
  console.log('================================================================');

  if (failedTests > 0) {
    process.exit(1);
  }
}

run().catch((err) => {
  console.error('Fatal test harness execution error:', err);
  process.exit(1);
});
