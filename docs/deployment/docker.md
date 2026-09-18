# Docker & Container Production Deployment Guide

![Docker](https://img.shields.io/badge/Docker-Container%20Image-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![GHCR](https://img.shields.io/badge/GHCR-ghcr.io%2Fspelech%2Fmodel--context--gateway-blue?style=for-the-badge&logo=github&logoColor=white)
![Security](https://img.shields.io/badge/Security-AES--256--GCM-orange?style=for-the-badge&logo=shield&logoColor=white)

This guide details the container deployment lifecycle for **Model Context Gateway (MCG)** using Docker and Docker Compose. It covers official container image registries, production Docker Compose recipes, persistent volume management, and exhaustive environment variable configurations.

---

## 📦 Official Container Image & Tags

Model Context Gateway images are published to the GitHub Container Registry (GHCR):

```text
ghcr.io/spelech/model-context-gateway
```

### Available Image Tags

| Tag | Target Architecture | Description |
| :--- | :--- | :--- |
| `latest` | `linux/amd64`, `linux/arm64` | Latest stable production release. Optimized minimal ASP.NET Core 10 runtime. |
| `<version>` (e.g. `5.11.0`) | `linux/amd64`, `linux/arm64` | Pinned immutable semantic release tag. |
| `latest-full` | `linux/amd64`, `linux/arm64` | Includes extended toolchain dependencies (Node.js/npm, Python 3, uv) for local STDIO subprocess tools. |
| `<version>-full` | `linux/amd64`, `linux/arm64` | Pinned semantic release with full toolchain packages pre-installed. |

To pull the latest production image:
```bash
docker pull ghcr.io/spelech/model-context-gateway:latest
```

---

## 🛠️ Production Docker Compose Recipes

### Recipe 1: Standard Production (SQLite with Persistent Volume)

This is the recommended standard deployment for single-node installations. It features automatic AES-256-GCM envelope encryption, automatic schema generation, and Docker socket tool auto-discovery.

```yaml
services:
  mcg:
    image: ghcr.io/spelech/model-context-gateway:latest
    container_name: mcg
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DB_PROVIDER=sqlite
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/mcg.db
      - CORS_ALLOWED_ORIGINS=https://mcg.example.com,http://localhost:8080
      - OpenIddict__CertificatePath=/app/data/openiddict.pfx
      - Oidc__TrustedProxies=127.0.0.1,::1,10.0.0.1
      - STANDALONE_ALLOWED_NETWORKS=127.0.0.1,::1,192.168.1.0/24
    volumes:
      - mcg_data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock:ro
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 10s

volumes:
  mcg_data:
    name: mcg_production_data
```

---

### Recipe 2: Enterprise Production (Microsoft SQL Server & External Secrets)

For high-availability clusters requiring a centralized relational database and external master key injection via Docker secrets:

```yaml
services:
  mcg:
    image: ghcr.io/spelech/model-context-gateway:latest
    container_name: mcg-enterprise
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DB_PROVIDER=mssql
      - ConnectionStrings__DefaultConnection=Server=sql.internal.corp;Database=McpGatewayDb;User Id=mcg_user;Password=SuperSecurePassword123!;TrustServerCertificate=True;
      - MCG_MASTER_KEY_FILE=/run/secrets/mcg_master_key
      - CORS_ALLOWED_ORIGINS=https://mcp.internal.corp
      - OpenIddict__CertificatePath=/run/secrets/openiddict_cert
      - Oidc__TrustedProxies=10.200.0.10,10.200.0.11
      - Admin__GroupSid=S-1-5-21-3623811015-3361044348-30300820-1113
    secrets:
      - mcg_master_key
      - openiddict_cert
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

secrets:
  mcg_master_key:
    file: ./secrets/master.key
  openiddict_cert:
    file: ./secrets/openiddict.pfx
```

---

### Recipe 3: Reverse Proxy Integration (Caddy with Automatic HTTPS & OIDC)

When fronting MCG with an external reverse proxy (Caddy, Nginx, or Traefik), you must configure `Oidc__TrustedProxies` to ensure incoming identity headers (`X-Forwarded-User`, `X-Forwarded-Email`, `X-Forwarded-Groups`) are validated rather than stripped.

```yaml
services:
  caddy:
    image: caddy:2-alpine
    container_name: mcg-caddy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    networks:
      - mcg_net

  mcg:
    image: ghcr.io/spelech/model-context-gateway:latest
    container_name: mcg-backend
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - DB_PROVIDER=sqlite
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/mcg.db
      - CORS_ALLOWED_ORIGINS=https://mcp.yourdomain.com
      - Oidc__TrustedProxies=mcg-caddy
    volumes:
      - mcg_data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock:ro
    networks:
      - mcg_net

networks:
  mcg_net:
    name: mcg_network

volumes:
  mcg_data:
  caddy_data:
  caddy_config:
```

**Corresponding `Caddyfile`:**
```caddyfile
mcp.yourdomain.com {
    reverse_proxy mcg-backend:8080 {
        # Preserve SSE streaming without buffering
        flush_interval -1
    }
}
```

---

## 💾 Persistent Volume Storage (`/app/data`)

Model Context Gateway stores stateful configuration, encryption keys, and SQLite records in the `/app/data` directory inside the container (or `/data` if mapped).

> [!CAUTION]
> **Data Loss Warning:**
> You **MUST** mount a persistent Docker volume or host directory to `/app/data`. If this mount is omitted, destroying or recreating the container deletes your database, active client AppKeys, audit logs, and encryption master key!

### Critical Files Stored in the Data Volume

| File | Purpose | Security Permissions |
| :--- | :--- | :--- |
| `mcg.db` | SQLite database holding servers, settings, access policies, AppKeys, audit logs, and encrypted credentials. | Read/Write by application process. |
| `.master.key` | Auto-generated 256-bit AES master encryption key (when not injected via environment or file secret). | `chmod 0600` (Owner read/write only). |
| `.admin.key` | Initial administrator token (`mcp-adm-...`) for bootstrap management via Web UI or `/admin`. | `chmod 0600`. |
| `.client.key` | Initial global client token (`mcp-glb-...`) for immediate AI client connection. | `chmod 0600`. |
| `openiddict.pfx` | PKCS#12 certificate for signing OAuth2/OIDC dynamic client registration tokens. | Secure certificate storage. |

---

## ⚙️ Production Environment Variables Reference

The following table provides the exhaustive list of configuration parameters accepted by Model Context Gateway:

| Parameter Key | Environment Variable Equivalent | Default Value | Description |
| :--- | :--- | :--- | :--- |
| **`ASPNETCORE_ENVIRONMENT`** | `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment profile (`Production` or `Development`). |
| **`MCG_MASTER_KEY`** | `MCG_MASTER_KEY` | *(Auto-generated)* | 256-bit base64-encoded key used for AES-256-GCM envelope encryption. |
| **`MCG_MASTER_KEY_FILE`** | `MCG_MASTER_KEY_FILE` | *(None)* | Path to file containing 256-bit master key (e.g. `/run/secrets/mcg_master_key`). |
| **`MCG_ADMIN_AUTH_KEY`** | `MCG_ADMIN_AUTH_KEY` (or `MCG_ADMIN_KEY`) | *(Auto-generated)* | Custom admin token or compact Base62 key (`mcp-adm-...`) seeded for user `admin`. |
| **`MCG_CLIENT_APP_KEYS`** | `MCG_CLIENT_APP_KEYS` | *(None)* | Pre-seeded client AppKeys in format: `key:Name:scope1,scope2`. |
| **`DB_PROVIDER`** | `DB_PROVIDER` | `sqlite` | Database engine dialect: `sqlite`, `mssql`, or `mysql`. |
| **`ConnectionStrings:DefaultConnection`** | `ConnectionStrings__DefaultConnection` | `Data Source=/app/data/mcg.db` | ADO.NET connection string for database provider. |
| **`CORS_ALLOWED_ORIGINS`** | `CORS_ALLOWED_ORIGINS` | `""` | Delimited list of allowed web browser origins (commas, semicolons). Empty blocks browser CORS in production. |
| **`OpenIddict:CertificatePath`** | `OpenIddict__CertificatePath` | *(Auto-generated ephemeral)* | File path to PKCS#12 (`.pfx`) certificate used to sign OAuth tokens across restarts. |
| **`Oidc:TrustedProxies`** | `Oidc__TrustedProxies` | `127.0.0.1,::1` | Delimited IP addresses or DNS hostnames of trusted reverse proxies. Requests from untrusted IPs have SSO headers stripped. |
| **`STANDALONE_ALLOWED_NETWORKS`** | `STANDALONE_ALLOWED_NETWORKS` | `127.0.0.1,::1` | Comma-separated list of IP subnets granted zero-auth Administrator access in Standalone Mode. |
| **`Admin:GroupSid`** | `Admin__GroupSid` | `S-1-5-32-544` | Security Identifier (SID) representing enterprise administrator role. |
| **`VAULT_ADDR`** | `VAULT_ADDR` | *(None)* | Base URL of HashiCorp Vault server (e.g. `http://vault:8200`). |
| **`VAULT_TOKEN`** | `VAULT_TOKEN` | *(None)* | Authentication token for HashiCorp Vault API. |
| **`Logging:LogLevel:Default`** | `Logging__LogLevel__Default` | `Information` | Serilog/Microsoft logging verbosity (`Trace`, `Debug`, `Information`, `Warning`, `Error`). |

---

## 📋 Step-by-Step Production Deployment Walkthrough

Follow these 6 steps to bootstrap a secure, enterprise-grade Docker deployment:

### Step 1: Generate the Master Encryption Key
`MCG_MASTER_KEY` protects backend credentials at rest using AES-256-GCM envelope encryption. Generate a cryptographically secure 256-bit key:
```bash
openssl rand -base64 32
```
Save this key securely.

### Step 2: Generate the OpenIddict Signing Certificate
In production mode, generating an explicit signing certificate ensures active client OAuth tokens remain valid across container restarts:
```bash
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 3650 -nodes -subj "/CN=mcg"
openssl pkcs12 -export -out openiddict.pfx -inkey key.pem -in cert.pem -passout pass:
```
Move `openiddict.pfx` into your persistent `./data` folder and set `OpenIddict__CertificatePath=/app/data/openiddict.pfx`.

### Step 3: Populate the `.env` Configuration File
Create a `.env` file adjacent to your `docker-compose.yml`:
```bash
ASPNETCORE_ENVIRONMENT=Production
MCG_MASTER_KEY=k8A+b7F...<your_base64_key_from_step_1>...=
DB_PROVIDER=sqlite
ConnectionStrings__DefaultConnection=Data Source=/app/data/mcg.db
CORS_ALLOWED_ORIGINS=https://mcg.internal.example.com
OpenIddict__CertificatePath=/app/data/openiddict.pfx
Oidc__TrustedProxies=10.0.0.1,172.18.0.1
STANDALONE_ALLOWED_NETWORKS=127.0.0.1,::1,192.168.1.0/24
```

### Step 4: Ensure Persistent Storage Directory Exists
```bash
mkdir -p ./data
chmod 700 ./data
cp openiddict.pfx ./data/
```

### Step 5: Launch the Container
```bash
docker compose up -d
```

### Step 6: Verify Health & Liveness Probes
Verify that the service is running and healthy:
```bash
# Check container logs for Dapper initialization
docker compose logs -f mcg | grep -i "Initializing database via Dapper"

# Query the health probe
curl -s http://localhost:8080/health | jq .
```

*Expected JSON response:*
```json
{
  "status": "Healthy",
  "version": "5.11.0",
  "database": {
    "provider": "SQLite",
    "connected": true
  },
  "servers": {
    "total": 0,
    "healthy": 0
  },
  "sessions": {
    "active": 0
  }
}
```

> [!NOTE]
> **Automated Test Coverage**: The project CI pipeline executes 1,063 automated tests (810 backend xUnit tests and 253 frontend Vitest tests) verifying container initialization, schema migrations, AES-256-GCM envelope encryption, and OAuth dynamic client registration.
