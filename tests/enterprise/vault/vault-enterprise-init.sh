#!/bin/sh
set -e

echo "=== Initializing Enterprise HashiCorp Vault Secrets ==="

# Wait for Vault to be healthy
export VAULT_ADDR="${VAULT_ADDR:-http://127.0.0.1:8200}"
export VAULT_TOKEN="${VAULT_TOKEN:-root-enterprise-token}"

echo "Waiting for Vault at ${VAULT_ADDR}..."
until vault status > /dev/null 2>&1; do
  sleep 1
done

echo "Enabling AppRole authentication..."
vault auth enable approle || true

echo "Writing MCG policy..."
vault policy write mcg-enterprise-policy - <<EOF
path "secret/*" {
  capabilities = ["read", "list"]
}
EOF

echo "Configuring AppRole for MCG..."
vault write auth/approle/role/mcg-role \
  secret_id_ttl=60m \
  token_num_uses=0 \
  token_ttl=60m \
  token_max_ttl=120m \
  secret_id_num_uses=0 \
  policies="mcg-enterprise-policy" || true

vault write auth/approle/role/mcg-role/role-id role_id=mcg-enterprise-role-id || true
vault write auth/approle/role/mcg-role/custom-secret-id secret_id=mcg-enterprise-secret-id || true

# ------------------------------------------------------------------
# 1. Standard MCG User Secrets Path (Current Gateway Implementation)
# Path: secret/users/{username}/{serverId}, Key: "secret"
# ------------------------------------------------------------------
echo "Populating Standard MCG User Secrets..."
vault kv put secret/users/steve/slack \
  secret="xoxp-steve-slack-token-999" \
  client_id="slack-steve-client-id" \
  client_secret="slack-steve-client-secret" \
  team_id="T01234567"

vault kv put secret/users/alice/slack \
  secret="xoxp-alice-slack-token-888" \
  client_id="slack-alice-client-id" \
  client_secret="slack-alice-client-secret" \
  team_id="T01234567"

# ------------------------------------------------------------------
# 2. Enterprise Desired Path Structure (Highlighting Gateway Shortcoming)
# Path: secret/{company}/mcgateway/{user}/{app}
# ------------------------------------------------------------------
echo "Populating Enterprise Desired Multi-Tenant Path Structure..."
vault kv put secret/acme/mcgateway/steve/slack \
  client_id="slack-steve-client-id" \
  client_secret="slack-steve-client-secret" \
  access_token="xoxp-steve-slack-token-999" \
  auth_blob='{"access_token":"xoxp-steve-slack-token-999","team_id":"T01234567","scope":"channels:read,chat:write"}'

vault kv put secret/acme/mcgateway/alice/slack \
  client_id="slack-alice-client-id" \
  client_secret="slack-alice-client-secret" \
  access_token="xoxp-alice-slack-token-888" \
  auth_blob='{"access_token":"xoxp-alice-slack-token-888","team_id":"T01234567","scope":"channels:read,chat:write"}'

# ------------------------------------------------------------------
# 3. Downstream Service Secrets
# ------------------------------------------------------------------
echo "Populating Downstream Service Secrets..."
# Service A: AppKey MCP Server
vault kv put secret/services/internal-appkey-mcp \
  ApiKey="corp-internal-key-456"

# Service B: Custom Header MCP Server
vault kv put secret/services/custom-header-mcp \
  token="custom-corp-token-789"

# Service C: Internal IdP Token Exchange Client Credentials
vault kv put secret/oauth/mock-idp \
  client_id="mcg-gateway-client" \
  client_secret="gateway-secret-321"

echo "=== Vault Enterprise Initialization Complete ==="
