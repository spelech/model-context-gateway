# Platform Configuration & User Setup Flow

This document explains how to configure user authentication in Model Context Gateway (MCG). It covers identity providers, reverse proxies, and AppKeys.

### Core Concepts for Beginners
- **AppKey (Bearer Token)**: A persistent secret string that IDEs and scripts send to authenticate with the gateway.
- **Reverse Proxy**: A server that verifies user identity and forwards requests with identity headers.
- **Scope**: A permission tag that controls which tools and endpoints a key can access.

```mermaid
flowchart TD
    A[Admin Configuration] --> B{Configure Identity Providers}
    
    B -->|SSO / Proxy Auth| C[OidcIdentityProvider<br>Reads HTTP Headers]
    B -->|Active Directory| D[ActiveDirectoryIdentityProvider<br>LDAP/AD Verification]
    B -->|API Tokens| E[AppKeyIdentityProvider<br>Issues/Validates AppKeys]
    
    C --> F[Define Header Names<br>e.g. Remote-User, Remote-Groups]
    D --> G[Define LDAP Bind DN,<br>Domain, Base Search]
    
    F --> H[(Database: AuthProviderConfigs)]
    G --> H
    
    I[User / Admin / Group] --> J{Needs API Access?}
    J -->|Yes| K[Generate AppKey]
    J -->|No| L[Uses SSO Dashboard Access]
    
    K --> M[Set AppKey Prefix & Secret]
    M --> N[Assign Scopes <br>e.g. 'admin', '*']
    N --> O[Assign OwnerSid]
    O --> P[(Database: AppKeys)]
```

### Explanation of AppKeys

AppKeys are bearer tokens that external clients (such as IDEs or scripts) use to authenticate with the gateway.

- **Prefix & Hash**: AppKeys contain a public prefix and a secret part. The database indexes the prefix for lookup and stores only the SHA-256 hash of the secret. Token format: `mcp-{scopeSlug}-{selector}-{secret}`.
- **Scopes**: Scopes define key permissions. For example, `admin` or `*` grants administrative control over the gateway.
- **OwnerSid**: Binds the AppKey to a user account or group Security Identifier (SID). When an IDE connects with this key, the gateway applies the permissions of the assigned `OwnerSid`. Teams can use shared service account keys while maintaining role-based access control.
