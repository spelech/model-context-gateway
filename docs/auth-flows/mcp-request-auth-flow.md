# MCP Request Auth End-to-End Flow

This diagram shows the end-to-end authentication lifecycle of an MCP request. It traces the request from a client, through the gateway, to a downstream MCP server.

### Core Concepts for Beginners
- **Reverse Proxy**: A server that authenticates users and passes identity headers to the gateway.
- **Bearer Token**: A secret key sent in the `Authorization: Bearer <token>` header.
- **AuthShape**: The credential format required by a downstream server (Bearer token, Basic auth, custom header, or query parameter).

```mermaid
sequenceDiagram
    participant C as Client (IDE/LLM)
    participant R as Router Gateway
    participant DB as Router Database
    participant IDP as CompositeIdentityProvider
    participant SEC as CompositeSecretRetriever
    participant B as Backend MCP Server

    Note over C, B: Phase 1: Client to Router Authentication
    
    alt Uses AppKey (API Token)
        C->>R: HTTP Request (Header: Authorization / X-App-Key)
        R->>DB: Lookup AppKey by Prefix
        DB-->>R: AppKey Hash & Scopes
        R->>R: Validate SHA-256 Hash
    else Uses SSO/Proxy Header
        C->>R: HTTP Request (Headers: Remote-User, Remote-Groups)
        R->>IDP: Extract Identity from Headers
        IDP-->>R: Username & SIDs
    end
    
    R->>R: Establish Client Session & Claims (Username, SID, Scopes)
    
    Note over C, B: Phase 2: Router to Backend Authentication
    
    C->>R: Send MCP Protocol Message (e.g., tools/call)
    R->>DB: Fetch Backend Server Config
    DB-->>R: Server Details (SecretProvider, AuthShape)
    
    alt SecretProvider == UserProvided
        R->>DB: Fetch UserCredentialDto for (Username, ServerId)
        DB-->>R: Encrypted User Secret
        R->>R: Decrypt User Secret
    else SecretProvider == Vault / Environment / WindowsRegistry
        R->>SEC: GetSecretAsync(SecretPath, SecretKey)
        SEC-->>R: Global Target Secret
        R->>R: Cache Secret (5 mins)
    else SecretProvider == None
        R->>R: Use configured ApiKey (if any) or proceed without auth
    end
    
    Note over C, B: Phase 3: Proxying the Request
    
    R->>R: Format Secret using AuthShape (Bearer, Basic, Custom Header, etc.)
    R->>B: Forward HTTP/SSE Request with Injected Auth
    B-->>R: MCP Response
    R-->>C: Forward Response back to Client
```

### Auth & Setup Matrix Reference

This flow combines three security layers:
1. **Client Identity**: The gateway resolves caller identity from SSO headers or AppKey records.
2. **Server Auth Requirement**: The downstream server specifies its credential format (`AuthShape`).
3. **Secret Origin**: The gateway retrieves a shared secret (`Vault`, `Env`) or a personal secret (`UserProvided`). It formats the credential and forwards the request transparently.

> [!WARNING]
> **Dynamic Token Limitation**: The gateway does not mint dynamic tokens (such as short-lived JWTs) on behalf of users. Configured secret providers return static credentials.
>
> If a backend server requires a dynamic JWT, the client must obtain the token first. The client sends the token in the `X-Target-Auth` header. When the server has `AllowPassThroughAuth: true`, the gateway formats this token into the target `AuthShape` before forwarding the request.
