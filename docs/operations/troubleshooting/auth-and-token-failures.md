# Troubleshooting Authentication & Token Failures

[Home](../../index.md) > [Troubleshooting & RCA Overview](../../mcp-routing-and-admin-issues.md) > Auth & Token Failures

## 1. Overview

This runbook provides diagnostic and remediation steps for authentication failures, expired tokens, OIDC audience/issuer mismatches, AppKey scope rejection, and downstream secret provider errors in Model Context Gateway (MCG).

---

## 2. Common Authentication Failure Scenarios

| Failure Scenario | Error Code / Symptom | Primary Cause |
| :--- | :--- | :--- |
| **OIDC Audience Mismatch** | `401 Unauthorized: SecurityException: JWT audience 'api://wrong' does not match configured issuer/audience` | IdP token issued for wrong audience or scope |
| **OIDC Issuer Mismatch** | `SecurityException: Token issuer 'https://idp.old.com' does not match expected 'https://idp.new.com'` | Issuer mismatch per RFC 9207 security rules |
| **AppKey Expiration / Revocation** | `401 Unauthorized: AppKey expired on 2026-08-01` or `Key revoked` | AppKey expiration date reached or key explicitly revoked |
| **Insufficient AppKey Scope** | `403 Forbidden: AppKey scope 'server:docker' does not grant access to 'server:homeassistant'` | AppKey lacks target server, category, or tool scope |
| **Expired Vault / Secret Provider** | `401 Unauthorized: Downstream backend returned 401 Unauthorized` | HashiCorp Vault token expired or downstream API key rotated |

---

## 3. Diagnostic Procedures & Remediation

### Scenario 1: OIDC Audience and Issuer Mismatch
1. **Inspect Inbound JWT Claims**:
   Decode the Bearer token using `jwt.ms` or `jq` to verify `iss` (issuer) and `aud` (audience).
2. **Verify Gateway Configuration**:
   Ensure `Oidc:Authority` matches `iss` exactly (including trailing slashes) and `Oidc:Audience` matches `aud`.
   ```json
   {
     "Oidc": {
       "Authority": "https://authentik.corp.internal/application/o/mcg/",
       "Audience": "mcg-gateway-client-id"
     }
   }
   ```
3. **Issuer Binding Validation**:
   MCG strictly validates RFC 9207 issuer parameter (`iss`) on token exchange and proxy requests. Mismatched issuers trigger `SecurityException` and log an audit event (`sp_InsertAuditLog`).

### Scenario 2: AppKey Scope & Access Rejection
When an AppKey produces `403 Forbidden`:
1. Check AppKey prefix and scope definition using Admin tool `manage_appkeys(action: "get_limits", prefix: "mcp-usr-...")` or Web UI (**AppKeys** tab).
2. Ensure the key possesses one of the following required scopes:
   - `all` or `*`: Unrestricted access across all backend servers.
   - `server:<targetServerId>`: Explicit grant for target server.
   - `category:<categoryName>`: Grant for all servers in category.
   - `tool:<toolName>`: Grant for explicit tool.

### Scenario 3: Secret Provider & Vault Token Expiry
When downstream backend connections fail with 401 Unauthorized:
1. Test Vault connectivity using Admin tool `manage_providers(action: "test_vault")`.
2. Verify HashiCorp Vault AppRole token TTL and renewal settings. `CompositeSecretRetriever` automatically attempts token renewal before expiration.
3. If using `AesMasterKey` / local key file (`./data/.master.key`), ensure the file exists and permissions allow gateway read access.

---

## 4. Audit Log Querying for Auth Events

Query historical authentication failures via Admin MCP Server:
```json
{
  "name": "manage_system",
  "arguments": {
    "action": "query_audit",
    "category": "security",
    "limit": 50
  }
}
```

---

## 5. Related Operations Guides

* [**Troubleshooting & RCA Overview**](../../mcp-routing-and-admin-issues.md)
* [**Subprocess & STDIO Transports Runbook**](subprocess-and-stdio.md)
* [**Database Locks & Migration Recovery Runbook**](database-locks-and-migrations.md)
* [**AppKey Scopes & RBAC Guide**](../../appkey-scopes.md)
* [**Secret Providers & Key Management**](../../secret-providers.md)
