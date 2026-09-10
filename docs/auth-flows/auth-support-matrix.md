# Authentication End-to-End Support Matrix

This matrix defines the supported combinations of inbound client identities, outbound backend credentials, routing modes, and secret providers. It serves as the source of truth for understanding how the Router bridges authentication between clients (IDEs/LLMs) and downstream MCP Servers.

> [!TIP]
> **Looking for quick copy-paste setup recipes?** See the [**MCP Server Authentication & Integration Cookbook**](../mcp-server-auth-cookbook.md) (*"If Your Backend MCP Server Requires X ➔ Setup Is Y"*).

## 1. Outbound Secret Providers vs Routing Modes

This matrix evaluates the specific `SecretProvider` implementations available in the Router against the two core routing modes.

| Secret Provider | Description | Works in Proxy Route (`/{id}`) | Works in Meta-Routing (`/sse`) | Notes |
| :--- | :--- | :--- | :--- | :--- |
| `None` (Plaintext) | Fallback plaintext `ApiKey` stored in DB. | ✅ **Yes** | ✅ **Yes** | Global key. Not recommended for production. |
| `Environment` | Loads key from Host OS Env (`$ENV_VAR`). | ✅ **Yes** | ✅ **Yes** | Global key. Secure, immutable infrastructure. |
| `WindowsRegistry` | Loads key from DPAPI encrypted hive. | ✅ **Yes** | ✅ **Yes** | Global key. Secure Windows-native storage. |
| `Vault` | Fetches dynamic/static key from HashiCorp. | ✅ **Yes** | ✅ **Yes** | Global key. Supports auto-renewal & TTL. |
| `UserProvided` | Fetches PAT from user store (DB or Vault). | ✅ **Yes** | ✅ **Yes** | **User-Specific.** Router dynamically maps caller identity to personal credentials stored in database or HashiCorp Vault. |
| `TokenExchange` | Exchanges inbound identity/token via RFC 8693. | ✅ **Yes** | ✅ **Yes** | **User-Specific.** Exchanges caller token/identity with IdP for a scoped downstream bearer token. |
| `AllowPassThroughAuth`| Client sends dynamic JWT via `X-Target-Auth`. | ✅ **Yes** | ❌ **No** | **User-Specific.** Requires target-specific proxy route `/{serverId}`. Cannot be used in Meta-Routing because the client does not know which server will be invoked upfront. |

---

## 2. Authentication Formatting (AuthShapes) vs Transports

This matrix maps how the retrieved secret (from the providers above) is formatted and injected into the outbound transport.

| Transport Type | Configured `AuthShape` | Implementation Behavior (How the Secret is sent) | Supported? |
| :--- | :--- | :--- | :--- |
| `http`, `streamable`, `sse` | `bearer` | `Authorization: Bearer <secret>` header. | ✅ **Yes** |
| `http`, `streamable`, `sse` | `basic` | `Authorization: Basic <secret>` header. | ✅ **Yes** |
| `http`, `streamable`, `sse` | `raw` | `Authorization: <secret>` header. | ✅ **Yes** |
| `http`, `streamable`, `sse` | `x-api-key` | `X-API-Key: <secret>` header. | ✅ **Yes** |
| `http`, `streamable`, `sse` | `custom-header` | `<Custom-Name>: <secret>` header (using `SecretField`). | ✅ **Yes** |
| `http`, `streamable`, `sse` | `query` | Appends `?token=<secret>` (or custom name) to the URL. | ✅ **Yes** |
| `http`, `streamable`, `sse` | `impersonation` | Windows Kerberos identity delegation via `WindowsIdentity.RunImpersonated`. SecretProvider must be `None`. | ✅ **Yes** *(Windows Only)* |
| `stdio` | *(Ignored)* | `AuthShape` is ignored. Secret is injected securely into the process `EnvironmentVariables` (e.g., `API_KEY`). | ✅ **Yes** |

---

## 3. Inbound Identity vs Outbound Delegation

This matrix maps how the *inbound* identity (Client ➔ Router) can be propagated downstream (Router ➔ Backend).

| Inbound Identity Method | Outbound Downstream Method | Mechanism | Supported? |
| :--- | :--- | :--- | :--- |
| Active Directory / NTLM | Global API Key (Vault/Registry/Env) | Router trusts user, acts as Service Account. | ✅ **Yes** |
| Active Directory / NTLM | NTLM / Kerberos Impersonation | S4U2Proxy / `RunImpersonated` | ✅ **Yes** *(Windows Only)* |
| In-House IdP (External JWT) | Global API Key (Vault/Registry/Env) | Router validates JWT via JWKS, acts as Service Account. | ✅ **Yes** |
| In-House IdP (External JWT) | RFC 8693 Token Exchange | Router exchanges external JWT for scoped downstream JWT. | ✅ **Yes** |
| In-House IdP (External JWT) | User-Provided Secrets (Vault / DB) | Router maps JWT subject to user secrets in Vault or DB. | ✅ **Yes** |
| OIDC (HeaderProxy) | Global API Key (Vault/Registry/Env) | Router trusts SSO headers, acts as Service Account. | ✅ **Yes** |
| OIDC (HeaderProxy) | OAuth2 On-Behalf-Of (OBO) | Router exchanges tokens with Okta/Azure. | ✅ **Yes** |
| AppKey / OIDC / AD / JWT | HTTP Identity Header (`X-Forwarded-User`) | Router forwards resolved Username for RLS. | ✅ **Yes** |
| Interactive OAuth Consent | Any supported Outbound Method | Consent via React UI, Client calls via JWT, Router resolves subject. | ✅ **Yes** |

---

## 4. Enterprise Identity Delegation Capabilities

The gateway provides several mechanisms to bridge client identities and downstream authentication:

### Identity-Header Propagation (Trusted Gateway Pattern)
* **Mechanism**: Automatically injects the authenticated user's identity (e.g., `X-Forwarded-User: DOMAIN\User` or `X-Mcp-User: User`) into outgoing HTTP and SSE transport requests.
* **Benefits**: Downstream MCP servers enforce fine-grained Row-Level Security (RLS) and accurate audit logs without requiring downstream servers to validate complex tokens directly.

### Dynamic Token Exchange (OAuth 2.0 / OIDC On-Behalf-Of)
* **Mechanism**: The gateway acts as an OAuth 2.0 Confidential Client to exchange user tokens or AppKeys with Azure AD or Okta using standard On-Behalf-Of (OBO) flows.
* **Benefits**: Bridges static client AppKeys to dynamic downstream JWTs, allowing user-specific permissions to pass to protected backend services.

### Pre-Configured Runtimes for STDIO Tools
* **Mechanism**: The `ghcr.io/spelech/model-context-gateway:latest-full` Docker container includes Node.js, Python 3, `uv`, and `bun` pre-installed.
* **Benefits**: Run local script tools natively inside the container without building custom images or managing sidecar containers.

---

## 5. Dynamic Client Registration (DCR) & Per-User Consent

The router includes built-in support for RFC 7591 Dynamic Client Registration, allowing IDEs and autonomous agents (like Gemini Spark) to provision long-lived OAuth 2.0 credentials via the `/api/register` endpoint or the `manage_clients` MCP tool.

For complete specification, sequence diagrams, and integration guide, see:
* [**Dynamic Client Registration (RFC 7591) Guide**](dynamic-client-registration.md)
* [**Multi-Tenant OAuth Consent Flow**](multi-tenant-oauth-consent.md)

> **Interactive Per-User OAuth Consent Screen**: To support true multi-tenant scenarios (like the Slack MCP Server and Splunk MCP), the router natively supports standard `authorization_code` flows. When an AI IDE needs access to isolated backend resources on behalf of a user, it can trigger an interactive consent screen at `/connect/authorize`. This allows a user to explicitly grant the dynamically registered client access to their resources, returning an OIDC standard `authorization_code` to the IDE which can be exchanged for a short-lived access token, rather than relying strictly on static API Keys or proxy headers.

👉 For detailed architecture documentation, see **[Multi-Tenant OAuth Consent Flow & Dynamic Client Registration](multi-tenant-oauth-consent.md)**.

---

## 6. Downstream Authentication Mixing Guardrails

To prevent invalid, conflicting, or non-functional configurations, the system enforces the following matrix rules across the Web UI and API:

| Selection | Constraint / Enforced Behavior | Rationale |
| :--- | :--- | :--- |
| **`AuthShape: impersonation`** | Locks `SecretProvider` to `None`. Disables static `ApiKey` and `SecretKey`. | Outbound calls execute within `WindowsIdentity.RunImpersonated()` using the caller's Windows Kerberos ticket. External keys are redundant and invalid. |
| **`SecretProvider: UserProvided`** | Disables static `ApiKey`. SecretKey remains optional identifier. | Credentials resolve dynamically per user from the configured user secret store (`Database` or `Vault`). A static global key contradicts BYOK. |
| **`SecretProvider: TokenExchange`** | Requires `SecretKey` (downstream audience/scope). Incompatible with `impersonation`. | RFC 8693 token exchange mints an outbound Bearer JWT targeted for downstream microservices. |
| **User Secret Storage: `Vault`** | Requires Vault secret provider to be enabled. | The user store delegates credential storage to Vault KV v2 paths using configured path templates. |

---

## 7. Operational Considerations

1. **Format Translation**: Pass-Through authentication converts `X-Target-Auth` into the required backend format (`Authorization: Bearer <token>`) using `AuthShape`.
2. **STDIO Process Security**: For `stdio` servers, the gateway injects tokens into environment variables (`API_KEY`) rather than process command arguments.
3. **Container Runtimes for STDIO**: The `latest-full` container image contains Node.js, Python, `uv`, and `bun` for `stdio` MCP servers. Standard minimal images require external binaries.
