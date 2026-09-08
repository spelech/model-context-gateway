# Multi-Tenant OAuth Consent Flow & Dynamic Client Registration (DCR)

Model Context Gateway (MCG) supports standard OAuth 2.0 `authorization_code` and `refresh_token` flows. This architecture allows multi-tenant applications (such as Slack or Splunk integrations) to request access to user resources safely.

### Core Concepts for Beginners
- **OAuth 2.0**: An industry standard for authorization. It allows external applications to access resources on behalf of users without sharing credentials.
- **Bearer Token**: A security token. The client passes this token in the `Authorization: Bearer <token>` header to make requests.
- **Reverse Proxy**: A server that sits before the gateway. It handles user login and passes verified identity headers.

> [!TIP]
> **For the dedicated Dynamic Client Registration (RFC 7591) specification**, discovery metadata, and payload schemas, see the [**Dynamic Client Registration (RFC 7591) Guide**](dynamic-client-registration.md).

---

## 1. The Interactive Consent Architecture

The gateway functions as an **OAuth 2.0 Authorization Server** using OpenIddict. 

### The React Consent Screen
When an external client (such as Slack) starts an authorization request, it redirects the user to `/connect/authorize`. The gateway handles OpenIddict state and redirects the browser to `/consent`.

The React frontend displays the consent screen:
* Shows the registered `client_name`.
* Summarizes the requested MCP access.
* Provides **Accept** and **Deny** buttons that send a `POST` request back to `/connect/authorize`.

---

## 2. Inbound User Authentication Strategies

When the browser opens the consent screen, the gateway verifies user identity through one of two methods:

### Option A: OIDC / Zero-Trust Reverse Proxy (e.g., GWS-MCP, Cloudflare Access)
In cloud deployments, the gateway runs behind an identity-aware reverse proxy:
1. The proxy intercepts the request and requires the user to log in.
2. The proxy forwards the request with identity headers (`X-Forwarded-User` or `X-Amzn-Oidc-*`).
3. The gateway authenticates the user immediately from the headers and displays the consent UI.

### Option B: Enterprise Internal Network (Windows Authentication / AD)
On internal corporate networks without an external proxy:
1. The browser negotiates authentication using NTLM or Kerberos.
2. The ASP.NET Core pipeline creates a `WindowsPrincipal`.
3. The gateway reads the user identity and displays the consent screen without prompting for credentials.

---

## 3. The Code Exchange & Refresh Tokens

After the user clicks **Accept**, the gateway generates a short-lived `authorization_code`. It redirects the browser to the client's `redirect_uri`:

1. **Access Tokens**: The client calls `/connect/token` to exchange the code for a JWT Access Token. This token contains the user's identity in the `Subject` claim.
2. **Refresh Tokens**: The gateway issues a long-lived `refresh_token` (`AllowRefreshTokenFlow`). The client uses this token to renew expired access tokens silently.
3. **Execution**: The client sends the Access Token as a Bearer token with MCP requests. The gateway enforces access rules under the consenting user's identity.

---

## Summary of Client Registration & Persistence Isolation

OAuth 2.0 client records reside in a dedicated **`OAuthClients`** table. This isolates dynamic clients from static API keys (`AppKeys`):

* **SHA-256 Secret Hashing**: The gateway hashes the `client_secret` with SHA-256 immediately. The API returns the plaintext secret only once.
* **Privilege Decoupling**: Machine clients are created with `OwnerSid = ''`. They never inherit administrator SIDs.
* **Multi-Database Support**: The gateway supports SQLite, Microsoft SQL Server, and MySQL.

Client applications register with these default settings:
* `grant_types`: `["authorization_code", "refresh_token", "client_credentials"]`
* `response_types`: `["code"]`
* `token_endpoint_auth_method`: `"client_secret_post"` or `"client_secret_basic"`
* `redirect_uris`: Allowed callback destinations.

---

## 4. Step-by-Step Setup Guide (Example: Slack MCP Integration)

Follow these steps to configure an external multi-tenant AI client (such as Slack) with the Interactive Consent Flow:

### Step 1: Register the Dynamic Client
Register the application before you send users to the gateway.
Use the Web UI (**App Keys & Security > Dynamic Client Registration**) or send an HTTP request to `/api/register`:

```json
{
  "client_name": "Slack MCP Workspace App",
  "redirect_uris": ["https://slack.com/oauth/callback"],
  "grant_types": ["authorization_code", "refresh_token", "client_credentials"],
  "response_types": ["code"],
  "token_endpoint_auth_method": "client_secret_post"
}
```
**Save the Output**: The gateway returns HTTP `201 Created` with a `client_id` and a one-time plaintext `client_secret`.

### Step 2: Configure the Client (Slack App)
Enter these values in your Slack App portal:
1. **Client ID**: Enter the `client_id` from Step 1.
2. **Client Secret**: Enter the `client_secret`.
3. **Authorization URL**: Set to `https://your-router-url.com/connect/authorize`.
4. **Token URL**: Set to `https://your-router-url.com/connect/token`.
5. **Scopes**: Add the scope `api`.
6. **Redirect URI**: Note the callback URL provided by Slack (e.g., `https://slack.com/oauth/callback`).

### Step 3: Trigger the Flow
1. A user interacts with your Slack App and runs an MCP tool.
2. Slack detects that the user needs a token and displays a "Sign in to Router" button.
3. The user clicks the button, which opens the **Authorization URL** in the browser.

### Step 4: User Authentication & Consent
1. **Authentication**: If the gateway is behind a reverse proxy, the proxy authenticates the user. On an internal network, Windows Authentication logs the user in automatically.
2. **Consent**: The gateway renders the React consent screen (`/consent`). The user sees the requested permissions.
3. The user clicks **Authorize**.

### Step 5: Token Exchange & Execution
1. The gateway redirects the browser to Slack's **Redirect URI** with an `authorization_code`.
2. Slack calls the **Token URL** to exchange the code for an `access_token` and a `refresh_token`.
3. Slack executes MCP commands by sending `Authorization: Bearer <access_token>` with each request.
4. When the access token expires, Slack uses the `refresh_token` to obtain a new access token silently.
