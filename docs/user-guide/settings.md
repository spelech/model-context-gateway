# System Settings & Vector Embeddings

The **Settings View** (`Settings` tab) provides administrative controls for configuring vector search engines, identity and authentication providers, secret backends, custom tool specifications, and enterprise access control rules.

---

## 🧭 Settings Sub-Navigation Tabs

![System Settings Overview](../assets/settings_view.jpg)

The Settings interface is structured into five operational tabs:

```
+-------------------------------------------------------------------------------------------------------------------------+
| [ 🧠 Vector & Search ]  [ 🪪 Identity & Auth ]  [ 🔐 Secret Providers ]  [ 📂 Prompts & Resources ]  [ 👥 Access Control ] |
+-------------------------------------------------------------------------------------------------------------------------+
```

---

## 🧠 Tab 1: Vector & Search Engine (`GeneralTab`)

![Settings Vector and Semantic Search Options](../assets/settings_vector_search.jpg)

The vector embedding engine powers the **Meta-Mode** tool discovery function (`search_tools`). It generates mathematical representations of natural language intents and compares them against tool catalog metadata:

```
+-------------------------------------------------------------------------------+
| 🧠 Vector Embedding & Semantic Search Configuration                           |
+-------------------------------------------------------------------------------+
| Embedding Engine:     (•) Local ONNX In-Process   ( ) External API Provider   |
|                                                                               |
| [Local ONNX Engine Parameters]                                                |
| Model Architecture:   All-MiniLM-L6-v2 (384-dimensional dense vectors)        |
| Model Cache Path:     [ /data/models                                       ]  |
| Execution Provider:   CPU Multi-Threaded In-Process (Microsoft.ML.OnnxRuntime) |
|                                                                               |
| [External API Provider Parameters]                                            |
| Provider Type:        [ OpenAI / Compatible (LiteLLM, Ollama) ▾ ]             |
| Base API Endpoint:    [ https://api.openai.com/v1                          ]  |
| API Secret Key:       [ sk-proj-********************************           ]  |
| Model Identifier:     [ text-embedding-3-small                             ]  |
|                                                                               |
| [ Save Vector Settings ]                                                      |
+-------------------------------------------------------------------------------+
```

### 1. Local ONNX Engine (Recommended / Default)
* **Model Architecture**: Uses the embedded `All-MiniLM-L6-v2` transformer model (384-dimensional dense vectors).
* **Zero External Dependencies**: Executes entirely in-process on CPU via `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers`.
* **Data Privacy Guarantee**: Tool queries and schema embeddings never leave the local container or host.
* **Automatic Provisioning**: MCG downloads, validates SHA-256 checksums, and caches model weights in `/data/models` automatically.

### 2. External API Provider (OpenAI / Ollama / LiteLLM)
* **Usage**: Offloads embedding generation to remote cloud APIs or self-hosted GPU inference clusters.
* **Supported Backends**: OpenAI (`text-embedding-3-small`, `text-embedding-ada-002`), Azure OpenAI, Ollama, LiteLLM, Open WebUI, and vLLM.
* **Key Encryption**: API keys are encrypted at rest in the database using AES-256-GCM envelope encryption.

---

## 🪪 Tab 2: Identity & Auth Providers (`IdentityAuthTab`)

![Settings Identity and Authentication Providers](../assets/settings_identity_auth.jpg)

Manage how MCG authenticates incoming client connections and extracts identity claims:

### 1. Active Directory / Windows SID Provider
* Enable or disable native Windows Kerberos and NTLM token negotiation.
* Configure Domain Controller hostnames, Base DN paths, and service account credentials.

### 2. OIDC / Reverse Proxy Headers Provider
* Enable or disable forward-auth header extraction (Authentik, Authelia, PocketID, Keycloak, Traefik, Caddy, Nginx).
* Configure custom header names: `Remote-User`, `Remote-Groups`, `Remote-Email`, and `Remote-Name`.

### 3. OpenIddict OAuth 2.0 Authorization Server
* Configure token lifetimes, including access token validity and refresh token rotation policies.
* Specify paths for X.509 signing certificates and encryption keys.

---

## 🔐 Tab 3: Secret Providers (`SecretProvidersTab`)

![Settings Enterprise Secret Providers](../assets/settings_secret_providers.jpg)

Configure external credential vaults that MCG queries at runtime:

* **HashiCorp Vault KV v2**: Configure Vault URL, AppRole credentials (`roleId` and `secretId`), or direct Vault Token with automatic renewal.
* **Windows Registry**: Configure base keys (`HKLM` / `HKCU`) and paths for DPAPI-encrypted secrets.
* **Container Environment**: Map host or container environment variables.
* **OAuth 2.0 Token Exchange (RFC 8693 / PocketID)**: Configure downstream token delegation and impersonation.

---

## 📂 Tab 4: Prompts & Resources File Manager (`CustomFilesTab`)

![Settings Prompts and Resources File Manager](../assets/settings_prompts_resources.jpg)

Create, edit, and manage custom JSON files that define virtual tools, prompt templates, and virtual resources:

* **File Catalog Table**: Displays registered specification files by filename, type (`Tools`, `Prompts`, `Resources`), and last updated timestamp.
* **Interactive JSON Editor**: Integrated Monaco/CodeMirror editor that validates JSON syntax before writing to disk or database.
* **Hot Reloading**: Saving changes immediately updates in-memory caches and triggers semantic vector re-indexing.

---

## 👥 Tab 5: Access Control & Group Mappings (`AccessControlTab`)

![Settings Access Control and Group Mappings](../assets/settings_access_control.jpg)

Configure fine-grained access rules and map external directory groups:

* **Group Mappings Table**: Maps external directory groups (e.g. `CN=IT-Admins,OU=Groups,DC=corp` or `S-1-5-21-1001`) to internal MCG roles (`full_admin`, `user`).
* **Server Policies Table**: Central policy management matrix defining target identifiers (`server:docker`, `tool:docker__ps`), required security groups, and evaluation modes (`ALLOW` or `DENY`).
