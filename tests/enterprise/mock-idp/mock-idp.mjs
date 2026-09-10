#!/usr/bin/env node
import http from 'http';
import crypto from 'crypto';
import { parse as parseQuery } from 'querystring';

const PORT = process.env.PORT || 8095;

// 1. Generate RSA 2048 keypair on startup for RS256 JWT signing
const { publicKey, privateKey } = crypto.generateKeyPairSync('rsa', {
  modulusLength: 2048,
  publicKeyEncoding: { type: 'spki', format: 'pem' },
  privateKeyEncoding: { type: 'pkcs8', format: 'pem' }
});

const pubKeyObj = crypto.createPublicKey(publicKey);
const jwk = pubKeyObj.export({ format: 'jwk' });
jwk.kid = 'mock-idp-key-1';
jwk.use = 'sig';
jwk.alg = 'RS256';

function base64UrlEncode(str) {
  return Buffer.from(str)
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}

function base64UrlDecode(str) {
  let base64 = str.replace(/-/g, '+').replace(/_/g, '/');
  while (base64.length % 4) {
    base64 += '=';
  }
  return Buffer.from(base64, 'base64').toString('utf8');
}

function signJwt(payload, expiresInSeconds = 3600) {
  const header = {
    alg: 'RS256',
    typ: 'JWT',
    kid: 'mock-idp-key-1'
  };

  const now = Math.floor(Date.now() / 1000);
  const fullPayload = {
    iss: `http://mock-idp:${PORT}`,
    iat: now,
    exp: now + expiresInSeconds,
    ...payload
  };

  const encodedHeader = base64UrlEncode(JSON.stringify(header));
  const encodedPayload = base64UrlEncode(JSON.stringify(fullPayload));
  const signatureInput = `${encodedHeader}.${encodedPayload}`;

  const signer = crypto.createSign('RSA-SHA256');
  signer.update(signatureInput);
  const signature = signer.sign(privateKey, 'base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');

  return `${signatureInput}.${signature}`;
}

export function verifyJwt(token) {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const [headerB64, payloadB64, signatureB64] = parts;

    const signatureInput = `${headerB64}.${payloadB64}`;
    let sigBase64 = signatureB64.replace(/-/g, '+').replace(/_/g, '/');
    while (sigBase64.length % 4) {
      sigBase64 += '=';
    }

    const verifier = crypto.createVerify('RSA-SHA256');
    verifier.update(signatureInput);
    const valid = verifier.verify(publicKey, Buffer.from(sigBase64, 'base64'));
    if (!valid) return null;

    const payload = JSON.parse(base64UrlDecode(payloadB64));
    const now = Math.floor(Date.now() / 1000);
    if (payload.exp && payload.exp < now) return null;

    return payload;
  } catch (err) {
    return null;
  }
}

const userDirectory = {
  steve: {
    username: 'steve',
    email: 'steve@corp.local',
    name: 'Steve Pelech',
    sid: 'S-1-5-21-1001',
    groups: ['MCP Developers', 'Slack Users'],
    group_sids: ['S-1-5-21-2001', 'S-1-5-21-2002']
  },
  alice: {
    username: 'alice',
    email: 'alice@corp.local',
    name: 'Alice Developer',
    sid: 'S-1-5-21-1002',
    groups: ['MCP Developers', 'Slack Users'],
    group_sids: ['S-1-5-21-2001', 'S-1-5-21-2002']
  },
  bob: {
    username: 'bob',
    email: 'bob@corp.local',
    name: 'Bob Operator',
    sid: 'S-1-5-21-1003',
    groups: ['MCP Operators'],
    group_sids: ['S-1-5-21-2003']
  },
  admin: {
    username: 'Administrator',
    email: 'admin@corp.local',
    name: 'System Administrator',
    sid: 'S-1-5-32-544',
    groups: ['Administrator', 'full_admin', 'MCP Administrators'],
    group_sids: ['S-1-5-32-544']
  }
};

const server = http.createServer((req, res) => {
  const host = req.headers.host || `localhost:${PORT}`;
  const urlObj = new URL(req.url, `http://${host}`);
  const pathname = urlObj.pathname;

  console.log(`[MOCK-IDP] ${req.method} ${pathname}`);

  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Headers', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');

  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  // 1. OIDC Discovery Document
  if (pathname === '/.well-known/openid-configuration') {
    const config = {
      issuer: `http://${host}`,
      authorization_endpoint: `http://${host}/oauth/authorize`,
      token_endpoint: `http://${host}/oauth/token`,
      userinfo_endpoint: `http://${host}/userinfo`,
      jwks_uri: `http://${host}/.well-known/jwks.json`,
      response_types_supported: ['code', 'token', 'id_token'],
      subject_types_supported: ['public'],
      id_token_signing_alg_values_supported: ['RS256'],
      grant_types_supported: [
        'authorization_code',
        'client_credentials',
        'urn:ietf:params:oauth:grant-type:token-exchange',
        'urn:ietf:params:oauth:grant-type:jwt-bearer'
      ],
      scopes_supported: ['openid', 'profile', 'email', 'internal-mcp', 'tools:execute']
    };
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(config, null, 2));
    return;
  }

  // 2. JWKS Public Keys
  if (pathname === '/.well-known/jwks.json' || pathname === '/.well-known/jwks') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ keys: [jwk] }, null, 2));
    return;
  }

  // 3. Token Endpoint (Client Credentials, RFC 8693 Token Exchange, RFC 7523 JWT Bearer)
  if (pathname === '/oauth/token' || pathname === '/connect/token') {
    if (req.method !== 'POST') {
      res.writeHead(405, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: 'method_not_allowed' }));
      return;
    }

    let rawBody = '';
    req.on('data', chunk => { rawBody += chunk; });
    req.on('end', () => {
      let params = {};
      const contentType = req.headers['content-type'] || '';
      if (contentType.includes('application/json')) {
        try { params = JSON.parse(rawBody); } catch (e) { params = {}; }
      } else {
        params = parseQuery(rawBody);
      }

      const grantType = (params.grant_type || '').toLowerCase();
      console.log(`[MOCK-IDP] Handling grant_type: ${grantType}`);

      let subject = params.subject || params.username || 'steve';
      let subjectToken = params.subject_token || params.assertion;

      if (subjectToken) {
        // If subjectToken is a JWT, parse claims
        const verified = verifyJwt(subjectToken);
        if (verified && (verified.sub || verified.preferred_username)) {
          subject = verified.preferred_username || verified.sub;
        } else if (typeof subjectToken === 'string' && !subjectToken.includes('.')) {
          subject = subjectToken;
        }
      }

      const user = userDirectory[subject.toLowerCase()] || {
        username: subject,
        email: `${subject}@corp.local`,
        name: subject,
        sid: 'S-1-5-21-9999',
        groups: ['MCP Users'],
        group_sids: ['S-1-5-21-2999']
      };

      const targetAudience = params.audience || params.resource || 'internal-mcp';
      const requestedScope = params.scope || 'internal-mcp tools:execute';

      const tokenPayload = {
        sub: user.username,
        preferred_username: user.username,
        name: user.name,
        email: user.email,
        aud: targetAudience,
        scope: requestedScope,
        sid: user.sid,
        group_sids: user.group_sids,
        groups: user.groups,
        roles: user.groups
      };

      const accessToken = signJwt(tokenPayload, 3600);

      const responsePayload = {
        access_token: accessToken,
        token_type: 'Bearer',
        expires_in: 3600,
        scope: requestedScope,
        issued_token_type: 'urn:ietf:params:oauth:token-type:access_token'
      };

      res.writeHead(200, {
        'Content-Type': 'application/json',
        'Cache-Control': 'no-store',
        'Pragma': 'no-cache'
      });
      res.end(JSON.stringify(responsePayload));
    });
    return;
  }

  // 4. UserInfo Endpoint
  if (pathname === '/userinfo') {
    const authHeader = req.headers['authorization'] || '';
    if (!authHeader.startsWith('Bearer ')) {
      res.writeHead(401, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: 'unauthorized' }));
      return;
    }

    const token = authHeader.substring(7);
    const verified = verifyJwt(token);
    if (!verified) {
      res.writeHead(401, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ error: 'invalid_token' }));
      return;
    }

    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(verified));
    return;
  }

  // 5. Health Check
  if (pathname === '/health') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ status: 'healthy', service: 'mock-inhouse-idp' }));
    return;
  }

  res.writeHead(404, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ error: 'not_found' }));
});

server.listen(PORT, () => {
  console.log(`[MOCK-IDP] In-House Mock IdP running on http://0.0.0.0:${PORT}`);
});
