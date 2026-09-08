# 🧩 Deployment and Authentication Support Matrix

This document lists supported combinations of hosting environments, authentication providers, and downstream delegation methods for Model Context Gateway (MCG). MCG manages and secures communication for the Model Context Protocol (MCP).

## 🏗️ Hosting and Authentication Matrix

| Hosting Environment | Identity or Authentication Mechanism | Application Key (AppKey) Support | Downstream Identity Delegation | Notes and Limitations |
| :--- | :--- | :--- | :--- | :--- |
| **Linux Container (Docker)** | **Reverse Proxy Headers** (OpenID Connect [OIDC], SAML) | ✅ Supported | **OAuth2 On-Behalf-Of JWT** or **Header Propagation** (`X-Forwarded-User`) | You must configure `Oidc:TrustedProxies`. Linux containers do not support Kerberos or NTLM impersonation (`S4U2Proxy`). |
| **Windows Native (IIS)** | **Active Directory (AD) / Windows Authentication** | ✅ Supported | **Kerberos / NTLM Impersonation** (`S4U2Proxy`) or **Header Propagation** | The Internet Information Services (IIS) application pool must run as a domain account. That account requires constrained delegation rights when using `S4U2Proxy`. |
| **Standalone (Kestrel)** | **AppKey Only** (Machine-to-Machine) | ✅ Supported | **None** (Executes in router context) | Use this option for automated agents or internal microservices that do not need user identity. |
| **Any Environment** | **Dynamic Authentication Pass-Through** | ✅ Supported | **Direct Target Authentication** (`X-Target-Auth`) | MCG intercepts HTTP 401 challenge codes from backend tools. It then prompts the client or Integrated Development Environment (IDE) for credentials. |

---

## 🔑 Authentication Context Limitations

Different authentication methods provide different capabilities and security constraints in MCG.

### 1. Reverse Proxy (OIDC / Header Authentication)
* **Bound Context:** Human User.
* **Capabilities:** 
  * Supplies fine-grained user identity to the gateway.
  * Maps external identities to internal administrator roles through `manage_group_mappings`.
  * Fully records and audits user activity.
* **Limitations:** 
  * Only accepts traffic when the reverse proxy IP address is listed in `Oidc:TrustedProxies`.
  * If requests bypass the reverse proxy, the gateway strips identity headers. Access falls back to Guest or Anonymous.

### 2. AppKey (Application Key) Authentication
* **Bound Context:** Machine or Autonomous Agent.
* **Capabilities:**
  * Delivers granular scope permissions (`server:*`, `category:*`, `tool:*`).
  * Operates on behalf of a specific owner security identifier (`OwnerSid`).
* **Limitations:**
  * Ignores reverse proxy Single Sign-On (SSO) headers.
  * Does not support interactive downstream SSO flows without an OAuth2 On-Behalf-Of token exchange.

### 3. Windows and Active Directory (IIS)
* **Bound Context:** Enterprise Domain User.
* **Capabilities:**
  * Delivers Zero-Trust security without external identity providers.
  * Inspects `WindowsIdentity` objects directly for fast access checks.
* **Limitations:**
  * Requires Windows Server and IIS.
  * Does not port easily to Linux Kubernetes clusters without complex LDAP sidecars or group Managed Service Accounts (gMSA).

---

## 🛡️ Downstream Delegation Support

When MCG forwards a request to a remote MCP server, it must authenticate downstream. The table below shows outbound delegation strategies for each inbound method.

| Inbound Auth Method | Outbound: Header Propagation | Outbound: OAuth2 On-Behalf-Of | Outbound: Kerberos Impersonation |
| :--- | :---: | :---: | :---: |
| **Proxy SSO Header** | ✅ Supported | ✅ Supported | ❌ Not Supported |
| **AppKey** | ⚠️ AppKey Owner Context | ❌ Not Supported (App is not a user) | ❌ Not Supported |
| **Windows Auth (AD)** | ✅ Supported | ❌ Not Supported (No JWT) | ✅ Supported (Windows Only) |

> 💡 **Recommendation:** For modern container deployments, use a **Reverse Proxy (OIDC)** with **Header Propagation** (`X-Forwarded-User`) or **OAuth2 Token Exchange**. Avoid Windows Authentication unless you maintain legacy domain-joined systems.
