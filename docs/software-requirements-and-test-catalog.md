# Software Requirements Specification (SRS) & Test Verification Catalog

> **Automated Verification Document:** Generated via `dotnet run --project scripts/CatalogGenerator`
> **Catalog Statistics:** **423 Requirements Verified** across **921 Test Proofs** (336 Functional Capabilities, 87 Safety Guardrails).

---

## 1. System Taxonomy & Verification Summary

| Category | Domain | Total Requirements | Positive Features | Guardrails / Fail-Closed | Verification Proofs |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **`API`** | API | **2** | 2 | 0 | 2 proofs |
| **`AUTH`** | Authentication, RBAC & Identity | **89** | 85 | 4 | 215 proofs |
| **`CORE`** | CORE | **8** | 7 | 1 | 14 proofs |
| **`DB`** | Multi-Database Persistence & Migrations | **23** | 22 | 1 | 35 proofs |
| **`DOC`** | DOC | **4** | 4 | 0 | 4 proofs |
| **`GUARD`** | Universal Safety & Fail-Closed Guardrails | **64** | 3 | 61 | 136 proofs |
| **`MCP`** | Model Context Protocol Engine & Tool Routing | **109** | 105 | 4 | 206 proofs |
| **`SEC`** | Secrets Providers & Encryption | **63** | 54 | 9 | 136 proofs |
| **`TRANS`** | Transports (SSE, HTTP, STDIO, Proxy) | **35** | 31 | 4 | 41 proofs |
| **`UI`** | Dashboard, Test Bench & Settings UI | **26** | 23 | 3 | 132 proofs |

---

## 2. Functional Requirements ("What the Application DOES")

### `[API-GET-SERVERS-FLEET-REPOSITORY]` Retrieves configured MCP server fleet from the database repository.
* **Category:** `API` (API)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MinimalApiEndpointsTests.cs#L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MinimalApiEndpointsTests.cs#L42) (`GetServers_Returns_Server_List`)

### `[API-SERVER-CRUD-LIFECYCLE]` Supports full CRUD lifecycle (create, read, update, delete) for downstream MCP server definitions.
* **Category:** `API` (API)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MinimalApiEndpointsTests.cs#L55`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MinimalApiEndpointsTests.cs#L55) (`Post_Put_Delete_Server_Lifecycle_Works`)

### `[AUTH-001]` Verify DatabaseUserSecretStore encrypts and decrypts secret correctly.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserSecretStoreTests.cs#L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserSecretStoreTests.cs#L8) (`DatabaseUserSecretStore_SavesAndRetrieves_Secret`)

### `[AUTH-002]` Verify UserCredentialsController returns configured server IDs.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserCredentialsControllerTests.cs#L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserCredentialsControllerTests.cs#L11) (`GetUserCredentials_ReturnsServerIds`)

### `[AUTH-02]` AppKey scopes restrict access precisely across all MCP capabilities and backend targets
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (49):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L242`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L242) (`Pairwise_AppKeyScopes_RestrictsAccessPrecisely`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L278`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L278) (`CreateAppKey_CreatesNewKey_Successfully_WithDifferentScopeSlugs`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L391`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L391) (`GetAppKeysLimits_ReturnsLimitsAndCounts`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L197`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L197) (`AppKeysController_CreateAppKey_ValidCategory_Succeeds`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L281`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L281) (`ClientsController_CreateClient_ValidCategory_Succeeds`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L364`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L364) (`ClientSession_CategoryScope_AuthorizesMatchingServerTools_AndDeniesOthers`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L388`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L388) (`ClientSession_GroupAliasScope_AuthorizesIdenticallyToCategory`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L410`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L410) (`ClientSession_CategoryScope_IsCaseInsensitive`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L460`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L460) (`ClientSession_ResourcesAndTemplates_FilteredByCategoryScope`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L488`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L488) (`ClientSession_DynamicServerMembership_UpdatesAccessDynamically`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L524`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L524) (`ClientSession_MixedScopes_CombinesCategoryAndSpecificToolScopes`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L551`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L551) (`ClientSession_Complete_FiltersServerNamesByCategoryScope`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditSidAttributionTests.cs#L34`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditSidAttributionTests.cs#L34) (`AppKeyAuthenticationHandler_Emits_Sid_Claim_When_OwnerSid_Present`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditSidAttributionTests.cs#L69`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditSidAttributionTests.cs#L69) (`AppKeyIdentityProvider_ResolvesOwnerAndSid_FromHttpContextItems`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditSidAttributionTests.cs#L87`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditSidAttributionTests.cs#L87) (`AppKeyIdentityProvider_ReturnsAnonymous_WhenNoAppKey`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L86`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L86) (`AppKeys_PrefixLookup_WorksCorrectly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L122`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L122) (`AppKeys_KeyExpiration_CheckedCorrectly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L151`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L151) (`AppKeys_Limits_CheckWorks`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L189`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L189) (`AppKeys_Sha256Hashing_VerificationWorks`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L338`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L338) (`ServiceCollection_Resolves_VaultUserSecretStore_WhenConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L388`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L388) (`ExternalJwtAuthenticationHandler_Authenticates_Valid_Bearer_Jwt`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L48`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L48) (`Pipeline_QueryToken_MiddlewareBypass`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L319`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L319) (`Pipeline_AppKey_Create_And_Revoke`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L402`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L402) (`Pipeline_GET_AppKeys_Returns200`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L411`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L411) (`Pipeline_GET_AppKeysLimits_Returns200`)
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L252`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L252) (`AppKeyScopes_RestrictTargetAccessPrecisely`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L22`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L22) (`initializes with default state`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L57`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L57) (`handles fetch error gracefully without crashing`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L123`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L123) (`handles register error with toast and propagates error`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L195`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L195) (`handles delete failure with error toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L261`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L261) (`opens and closes add client modal and resets created result`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L316`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L316) (`initializes with default state`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L388`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L388) (`loads app key limits`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L402`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L402) (`handles fetch error gracefully without crashing`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L466`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L466) (`handles create key error with toast and throws`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L541`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L541) (`handles revoke failure with error toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L559`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L559) (`loads user quotas and updates store`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L577`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L577) (`handles fetchUserQuotas error gracefully without crashing`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L616`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L616) (`handles setUserQuota error with toast and throws`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L687`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L687) (`handles deleteUserQuota failure with error toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L705`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L705) (`opens and closes create modal and clears result`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientSetupGuide.test.tsx#L51`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientSetupGuide.test.tsx#L51) (`switches between format tabs (Standard, VS Code, Generic SSE)`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientSetupGuide.test.tsx#L78`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientSetupGuide.test.tsx#L78) (`switches server scope from all servers to individual server`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientSetupGuide.test.tsx#L97`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientSetupGuide.test.tsx#L97) (`updates domain when LAN or custom is chosen`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientSetupGuide.test.tsx#L122`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientSetupGuide.test.tsx#L122) (`toggles meta mode when server scope is all`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientSetupGuide.test.tsx#L143`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientSetupGuide.test.tsx#L143) (`populates app keys dropdown and injects selected key`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientSetupGuide.test.tsx#L166`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientSetupGuide.test.tsx#L166) (`copies configuration to clipboard and triggers success toast`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientModal.test.tsx#L15`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientModal.test.tsx#L15) (`renders nothing when isAddClientOpen is false`)
  - [Playwright E2E] [`frontend/e2e/multi-user-matrix.spec.ts#L70`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/multi-user-matrix.spec.ts#L70) (`AppKey Direct Context: connects with API key header identity`)

### `[AUTH-03]` Auth middleware allows bypass routes and extracts SSO headers in a case-insensitive manner.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (25):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L603`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L603) (`AuthMiddleware_CaseInsensitivity_BypassAndHeader_Check`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L319`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L319) (`Pairwise_SsoIdentityAndGroupMappings_EvaluateCorrectly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L330`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L330) (`HeaderIdentityProvider_DynamicallyLoadsAndAppliesDbConfig`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L53) (`GetAllProviders_ReturnsOkWithSecretAndAuthProviders`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L161`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L161) (`GetAuthProviders_ReturnsOkWithList`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L185`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L185) (`SaveAuthProvider_SavesSuccessfully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L10`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L10) (`OidcIdentityProvider_Parses_Remote_User_And_Groups_Headers`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L31`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L31) (`CompositeIdentityProvider_Falls_Back_To_Oidc_When_AD_Not_Authenticated`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L74`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L74) (`HeaderAuth_AllowsHeaders_ForTrustedProxy`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L97`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L97) (`OidcIdentityProvider_DoesNotMapAdminSid_ForAdminGroups`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditSidAttributionTests.cs#L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditSidAttributionTests.cs#L11) (`HeaderIdentityProvider_Extracts_RemoteUserSid_And_Populates_Sid`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L9`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L9) (`ProviderName_ReturnsComposite`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L17`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L17) (`ResolveIdentityAsync_ReturnsFirstNonAnonymousUser`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L38`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L38) (`ResolveIdentityAsync_FallsBackToAnonymous_WhenNoUserResolved`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L55`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeIdentityProviderTests.cs#L55) (`ResolveIdentityAsync_FallsBackToOidcProvider_WhenAnonymous`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L366`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L366) (`Pipeline_GET_Permissions_Mappings_Returns200`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L384`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L384) (`Pipeline_GET_Providers_Auth_Returns200`)
  - [Backend xUnit] [`ModelContextGateway.Tests/GroupMappingsAndSpecAuthTests.cs#L93`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GroupMappingsAndSpecAuthTests.cs#L93) (`GroupMapping_AllowsUser_WhenMappingResolvesToAllowedInternalGroup`)
  - [Backend xUnit] [`ModelContextGateway.Tests/GroupMappingsAndSpecAuthTests.cs#L111`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GroupMappingsAndSpecAuthTests.cs#L111) (`GroupMapping_AllowsUser_WhenOidcGroupMapsToAllowedInternalGroup`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L629`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L629) (`AuthMiddleware_Allows_SSO_Session_With_RemoteUser_Header`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L207`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L207) (`DeleteMapping_DeletesSuccessfully`)
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L60`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L60) (`handles provider fetch warnings gracefully when endpoints are unavailable`)
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L80`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L80) (`saves auth provider config and refreshes providers`)
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L109`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L109) (`handles auth provider save error and displays toast`)
  - [Playwright E2E] [`frontend/e2e/multi-user-matrix.spec.ts#L36`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/multi-user-matrix.spec.ts#L36) (`Operator Context: allows overview and testbench navigation with operator identity`)

### `[AUTH-04]` ActiveDirectoryIdentityProvider extracts Windows caller SIDs and security groups via IWindowsIdentityAccessor and augments with LDAP
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (16):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ActiveDirectoryWindowsIdentityTests.cs#L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ActiveDirectoryWindowsIdentityTests.cs#L12) (`ResolveIdentityAsync_ExtractsWindowsIdentitySids_ViaAccessor`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ActiveDirectoryWindowsIdentityTests.cs#L50`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ActiveDirectoryWindowsIdentityTests.cs#L50) (`ResolveIdentityAsync_AugmentsWithLdapSids_WhenLdapServiceProvided`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L365`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L365) (`LdapActiveDirectoryService_RespectsDisabledStatusInDatabase`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L11) (`ResolveUserSidsAsync_ReturnsEmpty_WhenLdapProviderDisabledInDb`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L59`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L59) (`ResolveUserSidsAsync_UsesCache_WhenCachedSidsExist`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L298`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L298) (`TestLdapConnection_ValidatesInputAndHandlesFailureGracefully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L55`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L55) (`ActiveDirectoryIdentityProvider_Resolves_Steve_Identity_And_Sids`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L89`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L89) (`ActiveDirectoryIdentityProvider_Augments_Steve_With_LdapGroupSids`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L23`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L23) (`ConvertSidBytesToString_FormatsValidBinarySid`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L34`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L34) (`ConvertSidBytesToString_ReturnsEmpty_OnInvalidBytes`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L42) (`ResolveUserSidsAsync_ReturnsEmpty_WhenUsernameEmpty`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L53) (`ResolveUserSidsAsync_ReturnsEmpty_WhenServerNotConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L79`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L79) (`ActiveDirectoryIdentityProvider_ReturnsAnonymous_WhenUntrustedProxy`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L96`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L96) (`ActiveDirectoryIdentityProvider_ReturnsAnonymous_WhenNotWindowsAuth`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L112`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L112) (`ActiveDirectoryIdentityProvider_ResolvesLdapSids_WhenLdapServiceProvided`)
  - [Playwright E2E] [`frontend/e2e/ldap-identity-and-auth-flow.spec.ts#L5`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/ldap-identity-and-auth-flow.spec.ts#L5) (`should configure LDAP identity provider, test connection, and save settings`)

### `[AUTH-05]` McpServer supports AllowPassThroughAuth flag
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpServerTests.cs#L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpServerTests.cs#L5) (`McpServer_Should_Have_AllowPassThroughAuth`)
  - [Playwright E2E] [`frontend/e2e/my-mcp-servers.spec.ts#L7`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/my-mcp-servers.spec.ts#L7) (`should render user provided servers and allow editing credentials with SQLite schema`)

### `[AUTH-06]` Transports use passThroughToken when AllowPassThroughAuth is true
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L208`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L208) (`Transports_Use_PassThroughToken_If_Allowed`)

### `[AUTH-101]` HTTP transport injects X-Forwarded-User header based on connected user identity.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityHeaderTests.cs#L9`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityHeaderTests.cs#L9) (`HttpTransport_InjectsXForwardedUserHeader`)

### `[AUTH-110]` CreateAppKey allows creating unlimited AppKeys when UserMaxKeys is set to 0.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L343`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L343) (`CreateAppKey_AllowsUnlimited_WhenLimitsAreZero`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L135`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L135) (`ApplyConfigurationResponseContext_SetsRegistrationEndpoint`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L51`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L51) (`GetClients_ReturnsOk_WithClientsAndMappedProperties`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L238`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L238) (`GetClients_NeverLeaksRawBearerSecretOrHash`)

### `[AUTH-118]` FindDcrClientAsync resolves existing DCR client matching client name and type.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L214`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L214) (`FindDcrClient_ReturnsMatchingClient`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L556`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L556) (`RegisterClient_DuplicateDcrRequest_ReusesExistingClientIdAndUpdatesRecord`)

### `[AUTH-119]` CleanupDcrClientsAsync prunes duplicate and expired dynamic client registrations across all database providers.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L237`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L237) (`CleanupDcrClients_PrunesDuplicateRegistrations_AndExpiredClients`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L333`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L333) (`CleanupClients_CallsRepoAndReturnsCleanedCount`)

### `[AUTH-14]` Tool execution catches 401 Unauthorized from downstream target servers and returns interactive auth remediation.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L211`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L211) (`ExecuteTargetToolAsync_Catches401_AndReturnsAuthPrompt`)

### `[AUTH-15]` OpenIddict initializes ephemeral development signing certificates in Development environment.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/OpenIddictProductionTests.cs#L30`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OpenIddictProductionTests.cs#L30) (`Development_WithNoCert_BootsOnDevCerts`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OpenIddictProductionTests.cs#L46`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OpenIddictProductionTests.cs#L46) (`Production_WithValidPfx_Boots`)

### `[AUTH-35]` Single-user homelab startup initializes SQLite, auto-generates Admin and Client AppKeys without PFX certificate requirements
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SingleUserHomelabTests.cs#L30`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L30) (`Homelab_ZeroConfigStartup_SeedsAdminAndClientKeys_AndPersistsFiles`)

### `[AUTH-36]` Pre-configured MCG_CLIENT_APP_KEYS seeds functional individualized client keys with custom scopes
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SingleUserHomelabTests.cs#L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L98) (`Homelab_PreConfiguredClientKeys_SeedsIndividualizedScopedKeys`)

### `[AUTH-37]` AppKeys with server and category scopes enforce precise tool execution boundaries
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SingleUserHomelabTests.cs#L168`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L168) (`AppKey_ScopeExtraction_ExtractsSemanticPrefixes`)

### `[AUTH-38]` LAN CIDR network configuration allows standalone web dashboard access from local subnet
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SingleUserHomelabTests.cs#L183`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L183) (`Standalone_LanCidr_GrantsAdminAccessToLocalSubnet`)

### `[AUTH-39]` Zero-config startup defaults enterprise auth providers and secret providers to disabled
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SingleUserHomelabTests.cs#L211`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L211) (`ZeroConfig_Startup_DefaultsEnterpriseProviders_ToDisabled`)

### `[AUTH-ADMIN-FULL-ACCESS]` Administrator identities bypass granular capability policies and have full access to all MCP methods.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L154`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L154) (`AdminBypass_AllowsAllCapabilities_EvenWithoutDbPolicies`)

### `[AUTH-ADMIN-POLICY-ALLOW-GROUPNAME]` AdminPolicy allows principal with configured Admin Group Name (e.g., full_admin)
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L13`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L13) (`AdminPolicy_Allows_Principal_With_AdminGroupName`)

### `[AUTH-ADMIN-POLICY-ALLOW-GROUPS-ARRAY]` AdminPolicy allows principal with configured Admin Groups array
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L81`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L81) (`AdminPolicy_Allows_Principal_With_ConfiguredAdminGroups`)

### `[AUTH-ADMIN-POLICY-ALLOW-SID]` AdminPolicy allows principal with configured Admin SID
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L47`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L47) (`AdminPolicy_Allows_Principal_With_AdminSid`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminPolicySidOnlyTests.cs#L58`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicySidOnlyTests.cs#L58) (`AdminPolicy_Allows_Principal_With_AdminSid`)

### `[AUTH-ADMIN-VALIDATE-GROUPNAME]` SecurityValidationHelper authorizes principals via Admin Group Name
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L246`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L246) (`SecurityValidationHelper_IsAdmin_AllowsAdminGroupName`)

### `[AUTH-ADMIN-VALIDATE-GROUPS-ARRAY]` SecurityValidationHelper authorizes principals via custom configured Admin:Groups array
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L277`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L277) (`SecurityValidationHelper_IsAdmin_AllowsCustomAdminGroupsArray`)

### `[AUTH-ADMIN-VALIDATE-MAPPED-GROUPS]` SecurityValidationHelper authorizes principals via mappedGroups database resolution
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L292`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L292) (`SecurityValidationHelper_IsAdmin_AllowsMappedGroups`)

### `[AUTH-ADMIN-VALIDATE-SID]` SecurityValidationHelper authorizes principals via Admin Group SID
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L228`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L228) (`SecurityValidationHelper_IsAdmin_RequiresAdminGroupSid`)

### `[AUTH-APPKEY-ADMIN-FUTURE-CATEGORY]` Admin callers can create forward-looking AppKeys for unconfigured categories
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L258`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L258) (`AppKeysController_CreateAppKey_UnknownCategory_Admin_Succeeds`)

### `[AUTH-APPKEY-ADMIN-QUOTA]` Administrator can create, update, and delete custom user quota overrides.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L403`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L403) (`QuotaEndpoints_Admin_CanManageCustomUserQuotas`)

### `[AUTH-APPKEY-ADMIN-SCOPE-ALLOW]` AppKeys with admin scope grant Administrator role and pass AdminPolicy.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L79`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L79) (`AppKey_WithAdminScope_GrantsAdminAccess`)

### `[AUTH-APPKEY-ITEMS-SCOPE-ALLOW]` SecurityValidationHelper recognizes admin scopes in HttpContext.Items.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L255`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L255) (`IsAdmin_AppKeyScopes_InHttpContextItems_ReturnsTrue`)

### `[AUTH-APPKEY-KEYTYPE-PERSISTENCE-FILTER]` IAppKeyRepository persists KeyType and filters keys by personal vs system
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L146`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L146) (`AppKeyRepository_SaveAndGet_PersistsKeyTypeAndFilters`)

### `[AUTH-APPKEY-WILDCARD-SCOPE-ALLOW]` AppKeys with wildcard scope '*' grant Administrator role and pass AdminPolicy.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L140`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L140) (`AppKey_WithWildcardScope_GrantsAdminAccess`)

### `[AUTH-COMPACT-APPKEY-TAXONOMY]` Generates compact ~32-character Base62 AppKeys with semantic prefixes.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L417`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L417) (`CreateCredentialAsync_GeneratesCompactKeysWithSemanticPrefixes`)

### `[AUTH-CUSTOM-ADMIN-KEY-SEEDING]` Seeds custom MCG_ADMIN_AUTH_KEY when provided in configuration.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L189`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L189) (`Startup_SeedsCustomAdminKey_WhenConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L241`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L241) (`Startup_SeedsCustomAdminKey_WhenMcgAdminKeyConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L292`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L292) (`Startup_UpdatesAdminKeyHash_WhenEnvironmentKeyChanges`)

### `[AUTH-MOCK-SIMULATE-UNAUTHORIZED]` MockDownstreamMcpServer simulates 401 Unauthorized status code for authentication testing.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L62) (`MockDownstreamMcpServer_Simulates401Unauthorized`)

### `[AUTH-OIDC-PRESERVE-GROUP-NAMES]` OidcIdentityProvider preserves group names without synthesizing Windows SIDs
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L307`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L307) (`OidcIdentityProvider_DoesNotGrantAdminSid_FromGroupOrUserNames`)

### `[AUTH-PERM-GET-MAPPINGS]` PermissionsController returns group mappings list with 200 OK.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L137`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L137) (`GetMappings_ReturnsOk`)

### `[AUTH-PERM-POLICY-LIST]` PermissionsController returns access policies list with 200 OK.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L48`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L48) (`GetPolicies_ReturnsOk`)

### `[AUTH-PERM-POLICY-REMOVE]` PermissionsController removes access policies.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L114`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L114) (`DeletePolicy_DeletesSuccessfully`)

### `[AUTH-PERSONAL-APPKEY-LIST]` Non-admin users can view their personal App Keys
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (6):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L125`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L125) (`GetAppKeys_NonAdmin_ReturnsOnlyPersonalKeys_ForCurrentUser`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L351`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L351) (`loads app keys and updates store`)
  - [Frontend Vitest] [`frontend/src/test/components/App.test.tsx#L81`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/App.test.tsx#L81) (`renders role-adaptive UI for non-admin user`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeysCard.test.tsx#L34`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeysCard.test.tsx#L34) (`renders role-adapted My App Keys view for non-admin user`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeysCard.test.tsx#L79`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeysCard.test.tsx#L79) (`renders keys list, copies config snippet, and revokes key`)
  - [Playwright E2E] [`frontend/e2e/personal-appkeys-and-quotas.spec.ts#L5`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/personal-appkeys-and-quotas.spec.ts#L5) (`Non-Admin Context: displays My App Keys navigation and personal quota indicator`)

### `[AUTH-PERSONAL-APPKEY-QUOTA-OVERRIDE]` Custom user quotas override default limit
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (6):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L223`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L223) (`CreateAppKey_CustomQuotaOverride_AllowsHigherLimit`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L594`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L594) (`sets user quota override and refreshes quota list`)
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTab.test.tsx#L6`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L6) (`renders GeneralTab with security default quota inputs and triggers save`)
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTab.test.tsx#L71`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L71) (`updates form state when settings prop changes`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeysCard.test.tsx#L222`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeysCard.test.tsx#L222) (`manages custom user quotas in admin quotas tab`)
  - [Playwright E2E] [`frontend/e2e/personal-appkeys-and-quotas.spec.ts#L135`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/personal-appkeys-and-quotas.spec.ts#L135) (`Admin Context: configures custom user quota override`)

### `[AUTH-PIPELINE-ADMIN-DASHBOARD]` Dashboard management API suite executes for authorized administrators.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L170`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L170) (`Pipeline_Dashboard_Management_Suite`)

### `[AUTH-PIPELINE-GET-CLIENTS]` GET /api/clients returns active client sessions with 200 OK.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L348`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L348) (`Pipeline_GET_Clients_Returns200`)

### `[AUTH-PIPELINE-GET-POLICIES]` GET /api/permissions/policies returns access policies with 200 OK.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L357`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L357) (`Pipeline_GET_Permissions_Policies_Returns200`)

### `[AUTH-PIPELINE-PERM-CRUD]` Permissions policy and group mapping CRUD endpoints manage RBAC rules.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L304`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L304) (`Pipeline_Permissions_Policy_And_Mapping_CRUD`)

### `[AUTH-PREFIX-EXTRACTION]` ExtractKeyPrefix parses semantic prefixes, Base62 selectors, and legacy tokens accurately.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L451`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L451) (`ExtractKeyPrefix_ExtractsSemanticAndLegacyPrefixesAccurately`)

### `[AUTH-QUERY-TOKEN-EXTRACTION]` Query string token middleware extracts access_token or token query parameter to Authorization header.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/EndpointAuthorizationTests.cs#L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EndpointAuthorizationTests.cs#L7) (`QueryStringTokenMiddleware_Extracts_AccessToken_To_AuthorizationHeader`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EndpointAuthorizationTests.cs#L45`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EndpointAuthorizationTests.cs#L45) (`QueryStringTokenMiddleware_Extracts_Token_To_AuthorizationHeader`)

### `[AUTH-RBAC-GROUP-ALLOW]` RBAC grants access when user claims match the required policy security group.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L94`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L94) (`RBAC_AllowsUser_WhenPolicyMatchesRequiredGroup`)

### `[AUTH-RBAC-PROMPT-FILTER]` prompts/list filters exposed prompts according to caller permissions.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L362`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L362) (`ListPromptsAsync_FiltersUnauthorizedPrompts`)

### `[AUTH-RBAC-RESOURCE-FILTER]` resources/list filters exposed resources according to caller permissions.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L405`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L405) (`ListResourcesAsync_FiltersUnauthorizedResources`)

### `[AUTH-RBAC-TEMPLATE-FILTER]` resources/templates/list filters exposed resource templates according to caller permissions.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L448`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L448) (`ListResourceTemplatesAsync_FiltersUnauthorizedTemplates`)

### `[AUTH-RBAC-TOOL-FILTER]` tools/list filters exposed backend tools according to caller permissions.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L317`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L317) (`ListToolsAsync_FiltersUnauthorizedTools`)

### `[AUTH-RBAC-TOOLS-FILTER]` tools/list filters exposed backend tools according to caller role and RBAC policy permissions.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L200`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L200) (`ToolsList_FiltersByAuthorization`)

### `[AUTH-SERVER-LEVEL-POLICY]` Server-level access policies authorize all child tools, prompts, and resources under that server.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L212`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L212) (`ServerLevelPolicy_AuthorizesAllCapabilitiesUnderServer`)

### `[AUTH-SSE-PER-MESSAGE-IDENTITY]` SSE streams re-validate caller identity and permissions per message payload.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L121`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L121) (`SSE_ValidatesIdentityPerMessage`)

### `[AUTH-STANDALONE-ADMINPOLICY-LOOPBACK-ALLOW]` AdminPolicy succeeds in standalone mode for unauthenticated loopback requests.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L176`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L176) (`AdminPolicy_StandaloneMode_LoopbackIp_PassesAdminPolicy`)

### `[AUTH-STANDALONE-CUSTOM-CIDR-ALLOW]` Standalone mode grants admin access to client IPs matching Admin:StandaloneAllowedNetworks CIDR ranges.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L35`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L35) (`IsAdmin_StandaloneMode_CustomCidr_ReturnsTrue`)

### `[AUTH-STANDALONE-LOOPBACK-ALLOW]` Standalone mode without external IDP grants admin access to loopback IP addresses.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L14`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L14) (`IsAdmin_StandaloneMode_LoopbackIp_ReturnsTrue`)

### `[AUTH-STORE-MAPPING-FETCH]` fetches group mappings and updates store
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L156`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L156) (`fetches group mappings and updates store`)

### `[AUTH-STORE-MAPPING-MODAL-TOGGLE]` handles mapping modal open and close
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L330`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L330) (`handles mapping modal open and close`)

### `[AUTH-STORE-MAPPING-SAVE]` saves a group mapping and closes mapping modal
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L171`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L171) (`saves a group mapping and closes mapping modal`)

### `[AUTH-STORE-POLICY-CREATE]` creates/saves a policy (ALLOW rule) and closes modal
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L53`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L53) (`creates/saves a policy (ALLOW rule) and closes modal`)

### `[AUTH-STORE-POLICY-FETCH]` fetches access policies and updates store
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L38`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L38) (`fetches access policies and updates store`)

### `[AUTH-STORE-POLICY-INIT]` initializes with empty policies and mappings
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L21`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L21) (`initializes with empty policies and mappings`)

### `[AUTH-STORE-POLICY-MODAL-TOGGLE]` handles policy modal open and close
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L314`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L314) (`handles policy modal open and close`)

### `[AUTH-SYSTEM-APPKEY-SEPARATION]` System keys are distinct and require admin permissions
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (10):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L151`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L151) (`SystemAppKeys_RequireAdmin_AndSeparateFromPersonalKeys`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L311`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L311) (`PersonalAppKey_WithAllScope_DoesNotGrantAdministratorRole`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L364`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L364) (`SystemAppKey_WithAdminScope_GrantsAdministratorRole`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L335`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L335) (`switches keyTypeTab between personal and system`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L369`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L369) (`fetches system-filtered app keys via query parameters`)
  - [Frontend Vitest] [`frontend/src/test/components/App.test.tsx#L15`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/App.test.tsx#L15) (`renders header, navigation tabs, and default overview dashboard for admin user`)
  - [Frontend Vitest] [`frontend/src/test/components/App.test.tsx#L36`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/App.test.tsx#L36) (`switches between tabs on navigation click`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeyModal.test.tsx#L31`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeyModal.test.tsx#L31) (`allows admin to select key type and create system app key`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeysCard.test.tsx#L166`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeysCard.test.tsx#L166) (`handles admin tab switching and username filtering`)
  - [Playwright E2E] [`frontend/e2e/personal-appkeys-and-quotas.spec.ts#L87`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/personal-appkeys-and-quotas.spec.ts#L87) (`Admin Context: manages segmented App-Level Keys and User Personal Keys`)

### `[UI-100]` initializes with empty providers
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L1) (`initializes with empty providers`)

### `[UI-101]` should initialize with default values
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useUserStore.test.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L1) (`should initialize with default values`)

### `[UI-114]` renders nothing when isPolicyModalOpen is false
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/PolicyModal.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L1) (`renders nothing when isPolicyModalOpen is false`)

### `[UI-120]` RBAC and SID mapping administration UI allows configuring role policies and SID associations
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/rbac-enforcement-flow.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/rbac-enforcement-flow.spec.ts#L1) (`should create, verify, and delete RBAC policy and SID mapping`)

### `[UI-123]` should open App Keys & Security view and display client setup controls
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/client-setup-and-appkeys.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/client-setup-and-appkeys.spec.ts#L1) (`should open App Keys & Security view and display client setup controls`)

### `[UI-125]` Admin role renders full administrative dashboard and server management controls
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/multi-user-matrix.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/multi-user-matrix.spec.ts#L1) (`Admin Context: renders full administrator view and privileged controls`)

### `[UI-127]` should navigate to settings permissions tab and open policy configuration modal
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/rbac-and-permissions.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/rbac-and-permissions.spec.ts#L1) (`should navigate to settings permissions tab and open policy configuration modal`)

### `[UI-129]` should create client application and generate AppKey with scope constraints
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/appkey-and-client-lifecycle.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/appkey-and-client-lifecycle.spec.ts#L1) (`should create client application and generate AppKey with scope constraints`)

### `[UI-AUTH-TAB-AD-TOGGLE]` Renders Active Directory disabled initially, toggles on and exposes fields.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/IdentityAuthTab.test.tsx#L12`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/IdentityAuthTab.test.tsx#L12) (`renders Active Directory disabled initially, toggles on and exposes fields`)

### `[UI-AUTH-TAB-LDAP-TEST]` Fills LDAP parameters and executes test connection.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/IdentityAuthTab.test.tsx#L46`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/IdentityAuthTab.test.tsx#L46) (`fills LDAP parameters and executes test connection`)

### `[UI-POLICY-MODAL-CANCEL-DISMISS]` closes modal on cancel click
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/PolicyModal.test.tsx#L87`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L87) (`closes modal on cancel click`)

### `[UI-POLICY-MODAL-CREATE-DEFAULTS]` renders create policy form with default inputs
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/PolicyModal.test.tsx#L28`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L28) (`renders create policy form with default inputs`)

### `[UI-POLICY-MODAL-EDIT-PREFILL]` renders edit policy form pre-filled with policy data
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/PolicyModal.test.tsx#L44`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L44) (`renders edit policy form pre-filled with policy data`)

### `[UI-USER-STORE-ERROR-FALLBACK]` handles error response gracefully and sets unauthenticated user state
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useUserStore.test.ts#L50`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L50) (`handles error response gracefully and sets unauthenticated user state`)

### `[UI-USER-STORE-HEALTH-VERSION]` successfully updates version and service from /health endpoint
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useUserStore.test.ts#L113`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L113) (`successfully updates version and service from /health endpoint`)

### `[UI-USER-STORE-LOAD-PROFILE]` successfully loads user profile from /api/me
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useUserStore.test.ts#L23`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L23) (`successfully loads user profile from /api/me`)

### `[UI-USER-STORE-NETWORK-FAILURE]` handles network failure gracefully
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useUserStore.test.ts#L69`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L69) (`handles network failure gracefully`)

### `[UI-USER-STORE-ROLE-EXTRACTION]` correctly handles non-admin user role extraction
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useUserStore.test.ts#L89`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L89) (`correctly handles non-admin user role extraction`)

### `[UI-USER-STORE-VERSION-FALLBACK]` keeps existing fallback version on error
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useUserStore.test.ts#L128`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L128) (`keeps existing fallback version on error`)

### `[CORE-101]` Auto-added requirement tracking
* **Category:** `CORE` (CORE)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SessionManagerTests.cs#L9`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SessionManagerTests.cs#L9) (`PerformanceMetrics_And_TotalRequests_IncrementCorrectly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SessionManagerTests.cs#L33`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SessionManagerTests.cs#L33) (`UpdateBackendStatus_TracksBackendHealth`)

### `[CORE-GATEWAY-METADATA-BUILD-INIT-REQUEST]` BuildInitializeRequest formats standard JSON-RPC 2.0 initialize request with dynamic protocol version.
* **Category:** `CORE` (CORE)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L116`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L116) (`BuildInitializeRequest_GeneratesValidJsonRpc`)

### `[CORE-GATEWAY-METADATA-CONSTANTS]` Metadata constants and assembly version return consistent non-empty identifiers.
* **Category:** `CORE` (CORE)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L133`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L133) (`MetadataConstants_ReturnExpectedValues`)

### `[CORE-GATEWAY-METADATA-EXTRACTION-JSONELEMENT]` ExtractRequestedProtocolVersion parses protocolVersion from JsonElement params object.
* **Category:** `CORE` (CORE)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L96`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L96) (`ExtractRequestedProtocolVersion_FromJsonElement_ParsesValidVersion`)
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L105`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L105) (`ExtractRequestedProtocolVersion_FromJsonElement_FallsBackToDefault`)

### `[CORE-GATEWAY-METADATA-EXTRACTION-STRING]` ExtractRequestedProtocolVersion parses protocolVersion from initialize request payload or isolated params.
* **Category:** `CORE` (CORE)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L68`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L68) (`ExtractRequestedProtocolVersion_FromString_ParsesValidVersion`)
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L82`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L82) (`ExtractRequestedProtocolVersion_FromString_FallsBackToDefault`)

### `[CORE-GATEWAY-METADATA-SUPPORTED-VERSIONS]` IsSupportedProtocolVersion validates supported protocol versions case-insensitively with whitespace trimming.
* **Category:** `CORE` (CORE)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L8) (`IsSupportedProtocolVersion_ReturnsTrue_ForKnownVersions`)
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L22`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L22) (`IsSupportedProtocolVersion_ReturnsTrue_ForNullOrWhitespace`)

### `[CORE-GATEWAY-METADATA-VERSION-NEGOTIATION]` NegotiateProtocolVersion canonicalizes casing for known protocol versions.
* **Category:** `CORE` (CORE)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L41`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L41) (`NegotiateProtocolVersion_ReturnsCanonicalVersion_WhenMatchFound`)
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L49`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L49) (`NegotiateProtocolVersion_FallsBackToDefault_WhenUnrecognized`)
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L57`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L57) (`NegotiateProtocolVersion_FallsBackToDefault_WhenNullOrEmpty`)

### `[DB-02]` MSSQL stored procedure scripts declare all required procedures and parameter contracts correctly
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L311`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L311) (`Mssql_Scripts_DeclareAllProceduresAndExpectedParameters`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L369`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L369) (`MySql_Scripts_DeclareAllProceduresWithP_PrefixParameters`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L708`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L708) (`Repositories_MySQL_AppKeyOperations_UseP_PrefixParameters`)
  - [Backend xUnit] [`ModelContextGateway.Tests/MySqlLiveIntegrationTests.cs#L25`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MySqlLiveIntegrationTests.cs#L25) (`MySql_LiveRepository_AppKeyAndSecretProviderLifecycle_Succeeds`)

### `[DB-07]` SQLite upgrade migration automatically provisions OAuthClients table on legacy database
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (10):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L433`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L433) (`Sqlite_UpgradeMigration_ProvisionsOAuthClientsTable`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L543`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L543) (`Mssql_Migration004_DeclaresOAuthClientsTableAndProcedures`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L565`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L565) (`MySql_Migration004_DeclaresOAuthClientsTableAndProcedures`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L71`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L71) (`SaveAndGetOAuthClientById_Success`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L108`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L108) (`SaveOAuthClient_UpdateExisting_Success`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L150`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L150) (`GetOAuthClients_ReturnsAllClientsOrderedByCreatedAt`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L177`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L177) (`DeleteOAuthClient_ExistingClient_ReturnsTrueAndRemovesClient`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L198`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L198) (`DeleteOAuthClient_NonExistentClient_ReturnsFalse`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L206`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L206) (`GetOAuthClientById_NonExistentClient_ReturnsNull`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L335`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L335) (`Seeder_Initializes_OAuthClients_Table`)

### `[DB-FACTORY-MSSQL-CONFIGURED]` DbConnectionFactory initializes MS SQL Server database connection using Microsoft.Data.SqlClient.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L46`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L46) (`Factory_Creates_MsSql_Connection_When_Configured`)

### `[DB-FACTORY-MYSQL-CONFIGURED]` DbConnectionFactory initializes MySQL database connection using MySqlConnector.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L28`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L28) (`Factory_Creates_MySql_Connection_When_Configured`)

### `[DB-FACTORY-SQLITE-DEFAULT]` DbConnectionFactory initializes SQLite database connection with SQLCipher encryption.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L10`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L10) (`Factory_Creates_Sqlite_Connection_By_Default`)

### `[DB-INITIALIZER-CRUD-CONTRACT]` DatabaseInitializer baseline schema supports CRUD operations across core domain tables.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseInitializerTests.cs#L71`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L71) (`InitializeDatabase_SupportsCrud_AcrossCoreTables`)

### `[DB-INITIALIZER-ENSURE-ALIAS]` EnsureAliasColumn safely adds Alias column if missing and is idempotent.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseInitializerTests.cs#L54`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L54) (`EnsureAliasColumn_AddsColumnIfMissing_AndIsIdempotent`)

### `[DB-INITIALIZER-IDEMPOTENCY]` DatabaseInitializer is idempotent and succeeds without error when executed multiple times.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseInitializerTests.cs#L40`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L40) (`InitializeDatabase_IsIdempotent_WhenCalledMultipleTimes`)

### `[DB-INITIALIZER-SCHEMA-BASELINE]` DatabaseInitializer creates all 12 canonical tables on a fresh SQLite database.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseInitializerTests.cs#L10`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L10) (`InitializeDatabase_CreatesAllExpectedTables_OnFreshSqliteConnection`)

### `[DB-MAPPING-SAVE-SQLITE]` PermissionsController persists group mappings to SQLite database.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L182`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L182) (`SaveMapping_SavesSuccessfully_OnSqlite`)

### `[DB-POLICY-SAVE-MYSQL]` PermissionsController persists access policy to MySQL database repository.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L91`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L91) (`SavePolicy_SavesSuccessfully_OnMySql`)

### `[DB-POLICY-SAVE-SQLITE]` PermissionsController persists access policy to SQLite database repository.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L80) (`SavePolicy_SavesSuccessfully_OnSqlite`)

### `[DB-PROVIDER-INSTANTIATION-DIALECTS]` DbConnectionFactory instantiates valid IDbConnection instances across sqlite, mysql, and mssql dialects.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L9`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L9) (`DbConnectionFactory_Instantiates_SupportedProviders`)

### `[DB-QUOTA-REPO-DELETE]` IUserQuotaRepository DeleteUserQuotaAsync removes user quota record
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L132) (`UserQuotaRepository_Delete_RemovesQuota`)

### `[DB-QUOTA-REPO-DI-REGISTRATION]` IUserQuotaRepository is registered in dependency injection and resolvable
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L199`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L199) (`DependencyInjection_RegistersIUserQuotaRepository`)

### `[DB-QUOTA-REPO-GET-ALL]` IUserQuotaRepository GetAllUserQuotasAsync retrieves all quotas ordered by username
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L98) (`UserQuotaRepository_GetAll_ReturnsAllUserQuotas`)

### `[DB-QUOTA-REPO-SET-GET]` IUserQuotaRepository persists user quota overrides and retrieves them correctly
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L85`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L85) (`UserQuotaRepository_SetAndGet_ReturnsPersistedQuota`)

### `[DB-QUOTA-REPO-UPDATE-CONFLICT]` IUserQuotaRepository SetUserQuotaAsync updates existing quota on conflict
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L117`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L117) (`UserQuotaRepository_Update_UpdatesExistingQuota`)

### `[DB-SEEDER-INIT-SETTINGS-PROVIDERS]` DatabaseSeederService initializes default router settings, provider configs, and schema.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L30`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L30) (`Seeder_Initializes_Default_Settings_And_Providers`)

### `[DB-SEEDER-ROUTER-DEFAULT-DATA]` DatabaseSeeder initializes default router tables, settings, and seed servers.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L53) (`DatabaseSeeder_SeedsDefaultData_Successfully`)

### `[DB-SQLITE-LEGACY-UPGRADE-MIGRATION]` SQLite auto-migration seamlessly upgrades legacy schema, encrypts plaintext secrets, and preserves data
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L29`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L29) (`Sqlite_UpgradeMigration_FromLegacySchema_PreservesDataAndPassesValidation`)

### `[DB-TYPEHANDLER-JSON-LIST-SERIALIZATION]` JsonListTypeHandler serializes and deserializes string collections to JSON text across database providers.
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L42) (`JsonListTypeHandler_SerializesAndDeserializes_StringLists`)

### `[DOC-SETUP-SKILL-FRONTMATTER]` mcg-setup skill frontmatter is valid YAML, specifies name, description starting with 'Use when...', and length is under 1024 characters
* **Category:** `DOC` (DOC)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SetupSkillTests.cs#L18`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L18) (`Skill_Frontmatter_IsValidAndWithinCharacterLimit`)

### `[DOC-SETUP-SKILL-MIRROR]` The mcg-setup skill and templates are mirrored 1:1 in .agents/skills/mcg-setup/
* **Category:** `DOC` (DOC)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SetupSkillTests.cs#L152`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L152) (`Skill_MirroredInAgentsDirectory`)

### `[DOC-SETUP-SKILL-TEMPLATES]` All scaffold templates exist, are non-empty, and contain required directives such as responseBufferLimit, MCG_MASTER_KEY, and ghcr.io/spelech/model-context-gateway
* **Category:** `DOC` (DOC)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SetupSkillTests.cs#L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L98) (`Templates_AreValidAndContainRequiredDirectives`)

### `[DOC-SETUP-SKILL-WORKFLOW]` mcg-setup skill contains all 6 required setup phases including environment probing, hosting platforms, env vs UI trade-offs, identity/network topology, artifact generation, and health/client configuration
* **Category:** `DOC` (DOC)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SetupSkillTests.cs#L44`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L44) (`Skill_ContainsAllRequiredPhasesAndComparisons`)

### `[GUARD-DISPOSED-01]` Stateless HTTP POST lifecycle: HTTP response completes, HttpContext is marked disposed, background backend initialization completes successfully without ObjectDisposedException.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L225`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L225) (`StatelessHttpLifecycle_DisposedHttpContext_CompletesInitializationWithoutThrowing`)

### `[GUARD-LDAP-FILTER-ESCAPE]` EscapeLdapFilter sanitizes and escapes special LDAP filter characters to prevent LDAP injection.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L10`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L10) (`EscapeLdapFilter_EscapesSpecialCharacters`)

### `[GUARD-SECURITY-IDENTIFIER-VALIDATION]` SecurityValidationHelper validates tool and prompt names against namespaced server identifiers.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L44`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L44) (`ValidateToolOrPromptName_ValidatesNames`)

### `[API-PIPELINE-GET-HEALTH]` GET /health returns gateway health status with 200 OK.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L438`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L438) (`Pipeline_GET_Health_Returns200`)

### `[API-PIPELINE-GET-SERVERS]` GET /api/servers returns backend servers list with 200 OK.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L339`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L339) (`Pipeline_GET_Servers_Returns200`)

### `[API-PIPELINE-GET-STATS]` GET /api/stats returns server statistics with 200 OK.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L429`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L429) (`Pipeline_GET_Stats_Returns200`)

### `[API-PIPELINE-GET-VERSION]` GET /api/version returns version information with 200 OK.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L330`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L330) (`Pipeline_GET_Version_Returns200`)

### `[API-PIPELINE-POST-MESSAGE-PROTOCOL]` Full end-to-end JSON-RPC session message suite executes over HTTP POST.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L142`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L142) (`Pipeline_POST_Message_FullProtocolSession_Suite`)

### `[API-PIPELINE-POST-SSE-PROTOCOL]` Full end-to-end JSON-RPC protocol suite executes across SSE pipeline.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L85`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L85) (`Pipeline_POST_Sse_JSONRPC_Full_Protocol_Suite`)

### `[API-PIPELINE-SERVER-CRUD]` Backend server CRUD pipeline endpoints persist and manage downstream servers.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L276`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L276) (`Pipeline_Server_CRUD_Endpoints`)

### `[HEALTH-PROBE-ALL-ENABLED-FLEET]` BackendHealthCheckService probes all enabled backend servers in the fleet.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L161`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L161) (`ProbeAllServersAsync_Probes_All_Enabled_Servers`)

### `[HEALTH-PROBE-CUSTOM-SERVER-CONNECTED]` BackendHealthCheckService marks registered custom servers as Connected.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L264`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L264) (`ProbeServerAsync_Sets_Connected_For_Custom_Server`)

### `[HEALTH-PROBE-DISABLED-SERVER]` BackendHealthCheckService marks disabled servers as Disabled.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L132) (`ProbeServerAsync_Sets_Disabled_When_Server_Not_Enabled`)

### `[HEALTH-PROBE-HTTP-CONNECTED-200]` BackendHealthCheckService marks server as Connected when downstream HTTP/SSE endpoint returns 200 OK.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L57`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L57) (`ProbeServerAsync_Sets_Connected_When_Endpoint_Responds_200`)

### `[HEALTH-PROBE-HTTP-FAILED-EXCEPTION]` BackendHealthCheckService marks server as Failed when downstream HTTP connection fails.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L97`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L97) (`ProbeServerAsync_Sets_Failed_When_Endpoint_Throws_Exception`)

### `[HEALTH-PROBE-STDIO-VALID-CONNECTED]` BackendHealthCheckService sets valid STDIO servers to Connected without making network HTTP probes.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L190`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L190) (`ProbeServerAsync_Sets_Connected_For_Valid_Stdio_Server_Without_Http_Probe`)

### `[MCP-01]` initializes with default state
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (29):**
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L24`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L24) (`initializes with default state`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L46`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L46) (`successfully loads servers and updates state`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L65`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L65) (`triggers batch reconnect when refreshAll is true`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L85`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L85) (`handles server fetch errors gracefully and shows error toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L105`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L105) (`creates a new server via POST when no id is present`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L142`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L142) (`updates an existing server via PUT when id is present`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L176`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L176) (`shows error toast when save fails`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L194`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L194) (`sends PUT request to update server enabled state and refreshes`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L216`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L216) (`handles toggle failure with error toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L232`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L232) (`sends reconnect POST request and shows info toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L252`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L252) (`handles reconnect failure with error toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L325`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L325) (`updates search query and resets page to 1`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L338`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L338) (`updates sortBy and groupBy`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L352`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L352) (`updates page and pageSize`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L367`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L367) (`toggles group collapse state`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L381`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L381) (`manages modal open/close actions`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L403`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L403) (`opens inspect modal and loads server inspection data`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L428`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L428) (`handles inspect failure with error toast`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L445`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L445) (`sets inspect active tab and search query`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerCard.test.tsx#L66`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L66) (`renders connecting/retrying state`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerCard.test.tsx#L83`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L83) (`renders failed state with retry button`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerCard.test.tsx#L106`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L106) (`renders disconnected state with connect button and hidden badge`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerCard.test.tsx#L129`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L129) (`renders disabled state`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L41`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L41) (`renders Add MCP Server form with default values when in add mode`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L62`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L62) (`renders Edit MCP Server form populated with server details when editing`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L83`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L83) (`switches to connection command when STDIO transport type is selected`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L104`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L104) (`shows custom header input when auth shape is custom-header or query`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L127`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L127) (`closes modal when cancel button or close X is clicked`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L147`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L147) (`submits form with correctly formatted payload including trimmed categories`)

### `[MCP-02]` All MCP protocol capabilities enforce caller role authorizations consistently
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L385`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L385) (`Pairwise_AllCapabilities_UnderCallerRoles_EvaluateCorrectly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L38`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L38) (`ListToolsAsync_ReturnsMetaTools_InMetaMode`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L59`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L59) (`InvalidateCache_ClearsPopulatedState`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L348`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L348) (`ToolListing_And_Remapping_Works_Correctly`)

### `[MCP-05]` ResourceRoutingManager returns all registered resources when search query is empty.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (10):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L8) (`SearchResourcesAsync_ReturnsAll_WhenQueryIsEmpty`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L23`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L23) (`SearchResourcesAsync_FiltersByQuery_MatchingNameOrDescription`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L41`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L41) (`ReadResourceAsync_LocalBuiltInResources_ReturnCorrectJson`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L82`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L82) (`ListResourceTemplatesAsync_ReturnsBuiltInTemplates`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L411`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L411) (`ResourceRouting_And_UriTranslation_Works_Correctly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L767`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L767) (`BuiltInResources_Templates_And_Autocompletion_Works_Correctly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L977`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L977) (`CustomUserPrompts_And_Resources_Work_Correctly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L61`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L61) (`ValidateResourceUri_ValidatesUris`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ResourceRoutingTests.cs#L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingTests.cs#L5) (`SearchResourcesAsync_FiltersResourcesCorrectly`)
  - [Frontend Vitest] [`frontend/src/test/components/ResourceTesterCard.test.tsx#L47`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ResourceTesterCard.test.tsx#L47) (`handles custom URI input and submit`)

### `[MCP-06]` prompts/list aggregates, namespaces, and routes prompts to target backends.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L499`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L499) (`PromptListAggregation_And_Routing_Works_Correctly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L853`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L853) (`MetaPrompts_Works_Correctly`)
  - [Frontend Vitest] [`frontend/src/test/components/PromptTesterCard.test.tsx#L53`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PromptTesterCard.test.tsx#L53) (`triggers arg change and form submit`)

### `[MCP-08]` completion/complete forwards prompt completions to backend when caller is authorized.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L473`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L473) (`CompleteAsync_ForPrompt_ForwardsToBackend_WhenAuthorized`)
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L531`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L531) (`CompleteAsync_ForResourceTemplate_ForwardsToBackend_WhenAuthorized`)
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L589`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L589) (`CompleteAsync_LogsTemplate_ReturnsOnlyAuthorizedServers`)

### `[MCP-10]` DockerAutoDiscoveryService handles missing Docker socket gracefully without throwing unhandled exceptions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (5):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L83`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L83) (`DockerAutoDiscovery_ScanContainers_HandlesMissingSocketGracefully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L46`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L46) (`Service_Initializes_With_Valid_Dependencies`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L80) (`ExecuteAsync_SkipsScan_WhenDockerSocketDoesNotExist`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L104`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L104) (`ParseDiscoveredServers_ParsesValidDockerContainerLabels`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L138`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L138) (`UpsertDiscoveredServers_AddsNewServers_AndDisablesStoppedServers`)

### `[MCP-12]` DynamicEmbeddingService retrieves and persists embedding provider configurations in Settings table.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (19):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L62) (`DynamicEmbeddingService_Gets_And_Saves_Settings`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L108`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L108) (`ReloadSettings_UpdatesSettingsAndActiveService`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L125`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L125) (`DynamicEmbeddingService_GetEmbeddingAsync_Uses_ApiProvider_When_Configured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L149`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L149) (`CosineSimilarity_Calculates_Correct_Vector_Distance`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L173`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L173) (`PreWarmAsync_Executes_Without_Throwing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L199`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L199) (`GenerateEmbeddingAsync_Uses_UnderlyingProvider`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L7) (`Service_InitializesAndSetsUpPaths`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L20`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L20) (`ReloadSettings_ClearsSessionAndTokenizerState`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L36`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L36) (`CosineSimilarity_CalculatesOrthogonalAndIdenticalVectors`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L54`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OnnxEmbeddingServiceTests.cs#L54) (`GetEmbeddingAsync_ReturnsEmpty384Vector_ForEmptyString`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SemanticSearchServiceTests.cs#L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SemanticSearchServiceTests.cs#L42) (`SearchToolsSemanticAsync_ScoresAndRanksTools`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SemanticSearchServiceTests.cs#L61`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SemanticSearchServiceTests.cs#L61) (`SearchTools_KeywordMatching_WorksCorrectly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SemanticSearchServiceTests.cs#L78`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SemanticSearchServiceTests.cs#L78) (`SearchToolsSemanticAsync_FallsBackToKeyword_WhenEmbeddingServiceThrows`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L106`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L106) (`SemanticSearchService_Fallback_With_DummyEmbeddings`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EmbeddingServiceTests.cs#L61`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EmbeddingServiceTests.cs#L61) (`ApiEmbeddingService_GetEmbeddingAsync_Returns_Vector_From_OpenAI_Response`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L93`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L93) (`CallToolAsync_SearchTools_ReturnsSemanticResults`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L668`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L668) (`SemanticToolSearchRanking_Sorts_By_Score`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ApiEmbeddingServiceTests.cs#L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ApiEmbeddingServiceTests.cs#L5) (`CalculateCosineSimilarity_ComputesSimilarity`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ApiEmbeddingServiceTests.cs#L17`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ApiEmbeddingServiceTests.cs#L17) (`ReloadSettings_UpdatesSettings`)

### `[MCP-15]` All JSON-RPC results return a resultType discriminator (complete or input_required) per MCP 2026-07-28 spec.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L7) (`EnsureResultType_AttachesComplete_WhenMissing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L23`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L23) (`EnsureResultType_PreservesExistingResultType`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L39`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L39) (`EnsureResultType_HandlesNullResult`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L53) (`EnsureResultType_HandlesJsonElement`)

### `[MCP-21]` Admin endpoint handles direct Streamable HTTP POST tools/list request returning JSON even with Accept text/event-stream header.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (8):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L370`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L370) (`AdminEndpoint_DirectPost_ToolsList_ReturnsJson_EvenWithSseAcceptHeader`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L401`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L401) (`AdminEndpoint_DirectPost_Notification_ReturnsAccepted`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L421`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L421) (`TargetAdminEndpoint_DirectPost_ToolsList_ReturnsJson`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L8) (`Middleware_Parses_2026_Spec_Headers`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L36`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L36) (`Middleware_Falls_Back_To_Json_Body_When_Headers_Missing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L64`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L64) (`Middleware_Matches_Admin_And_Target_Proxy_Paths`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L86`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L86) (`Middleware_Detects_Notifications_Correctly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L103`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L103) (`Middleware_Skips_Non_Mcp_Paths`)

### `[MCP-23]` AdminMcpServer HandleInitializeAsync includes subscriptions capability in capabilities object.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L655`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L655) (`HandleInitializeAsync_Advertises_Subscriptions_Capability`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L667`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L667) (`ProcessRequestAsync_Handles_Subscriptions_Listen`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L194`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L194) (`Middleware_Parses_Subscriptions_Listen_Request`)

### `[MCP-24]` McpSpecMiddleware extracts OpenTelemetry W3C traceparent, tracestate, and baggage from headers and _meta.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L219`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L219) (`Middleware_Extracts_Trace_Context_From_Headers_And_Meta`)

### `[MCP-25]` ToolRoutingManager falls back to SessionManager global server tools cache during cold-start search_tools execution
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L264`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L264) (`CallToolAsync_SearchTools_FallsBackToGlobalSessionManagerCache_WhenLocalCacheEmpty`)

### `[MCP-26]` ToolRoutingManager normalizes tool name delimiters (slash and colon) to canonical double-underscore format.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L319`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L319) (`NormalizeTargetToolName_NormalizesSlashAndColonDelimiters`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L335`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L335) (`NormalizeTargetToolName_ResolvesBareToolName_WhenUnambiguous`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L348`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L348) (`NormalizeTargetToolName_ReturnsAmbiguityError_WhenToolExistsAcrossMultipleServers`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L368`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L368) (`SearchTools_ReturnsValidJsonArray_WhenNoToolsMatch`)

### `[MCP-27]` McpServer supports Alias property
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpServerTests.cs#L15`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpServerTests.cs#L15) (`McpServer_Supports_Alias_Property`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpServerTests.cs#L30`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpServerTests.cs#L30) (`DatabaseInitializer_EnsureAliasColumn_AddsColumnSuccessfully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L405`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L405) (`CacheTools_Exposes_Slash_Formatted_Name_With_Server_Alias`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L441`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L441) (`CacheTools_Exposes_Slash_Formatted_Name_With_Server_Id_When_Alias_Empty`)

### `[MCP-30]` IsUserAuthorizedAsync matches granular tool policies across /, :, and __ delimiters.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L284`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L284) (`IsUserAuthorizedAsync_MatchesToolPolicy_AcrossDelimiters`)
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L300`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L300) (`IsUserAuthorizedAsync_ResolvesAliasesAndServerIds_ForServerPolicies`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L466`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L466) (`NormalizeTargetToolName_Resolves_Multiple_Delimiters_And_Aliases`)

### `[MCP-ADMIN-ENDPOINT-CALL-TOOL]` Admin endpoint /admin/message executes tools/call for manage_system diagnostics.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L294`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L294) (`AdminEndpoint_SseSession_CallTool_ManageSystemDiagnostics`)

### `[MCP-ADMIN-ENDPOINT-HEAD-REQUEST]` Admin endpoint /admin handles HEAD request returning text/event-stream headers.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L212`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L212) (`AdminEndpoint_HeadRequest_ReturnsEventStreamHeaders`)

### `[MCP-ADMIN-ENDPOINT-LIST-TOOLS]` Admin endpoint /admin/message executes tools/list over active SSE session and returns 10 admin tools.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L224`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L224) (`AdminEndpoint_SseSession_ListTools`)

### `[MCP-ADMIN-ENDPOINT-ROUTER-ADMIN-TARGET]` Target proxy endpoint /router-admin routes directly to the Admin MCP server.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L149`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L149) (`TargetProxy_RouterAdmin_RoutesToAdminServer`)

### `[MCP-ADMIN-ENDPOINT-SSE-HANDSHAKE]` Admin endpoint /admin/sse performs initialize handshake with 2026-07-28 protocol version.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L62) (`AdminEndpoint_SseHandshake_NegotiatesProtocol`)

### `[MCP-ADMIN-INITIALIZE-HANDSHAKE]` AdminMcpServer initialize handles protocol negotiation for 2026-07-28 and 2024-11-05.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L199`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L199) (`HandleInitializeAsync_NegotiatesProtocolVersion`)

### `[MCP-ADMIN-PARITY-APPKEYS]` manage_appkeys supports full parity for list, get_limits, create, and revoke actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L370`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L370) (`ManageAppKeys_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-CLIENTS]` manage_clients supports full parity for register, list, and delete actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L423`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L423) (`ManageClients_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-CUSTOM-FILES]` manage_custom_files supports full parity for list, get, save, and delete prompt and resource files.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L716`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L716) (`ManageCustomFiles_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-GROUP-MAPPINGS]` manage_group_mappings supports full parity for list, save, and delete external-to-internal group mappings.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L530`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L530) (`ManageGroupMappings_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-JSONRPC-DISPATCH]` AdminMcpServer processes standard JSON-RPC 2.0 requests (tools/list, tools/call, ping).
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L873`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L873) (`AdminTools_ProcessRequest_JsonRpcProtocol`)

### `[MCP-ADMIN-PARITY-POLICIES]` manage_policies supports full parity for list, save, and delete access control policies.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L464`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L464) (`ManagePolicies_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-PROVIDERS]` manage_providers supports full parity for list, save_secret, test_vault, save_auth, and test_ldap actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L577`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L577) (`ManageProviders_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-SERVER-ALIAS]` AdminMcpServer manage_servers supports server alias for add, update, and list actions with collision validation.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L858`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L858) (`AdminMcpServer_ManageServers_Supports_Alias`)

### `[MCP-ADMIN-PARITY-SERVERS]` Validates that the manage_servers tool provides comprehensive administrative capabilities including listing, retrieving, creating, updating, toggling, deleting, and reconnecting servers.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L234`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L234) (`ManageServers_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-SETTINGS]` manage_settings supports full parity for get and update global router configurations.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L667`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L667) (`ManageSettings_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-SYSTEM]` manage_system supports full parity for diagnostics, get_logs, clear_logs, and query_audit actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L816`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L816) (`ManageSystem_Parity_AllActions`)

### `[MCP-ADMIN-PARITY-TEST-TOOL-CALL]` test_tool_call executes test bench backend tool calls and formats responses.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L846`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L846) (`TestToolCall_Execution_Parity`)

### `[MCP-ADMIN-PARITY-TOOLS-COVERAGE]` Ensures every UI management workflow is backed by a verified, equivalent action within the consolidated Admin MCP tools.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L191) (`AdminTools_ExecuteSuccessfully`)

### `[MCP-ADMIN-RECONNECT-ALL-PROPAGATION]` AdminMcpServer manage_servers reconnect_all triggers StartInitializationForBackend across active sessions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L730`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L730) (`ManageServers_ReconnectAll_TriggersSessionBackendInitialization`)

### `[MCP-ADMIN-REQUEST-WITHOUT-ID-NOT-DROPPED]` Admin endpoint executes requests without an ID and does not drop them as notifications.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L550`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L550) (`AdminEndpoint_DirectPost_RequestWithoutId_ExecutesSuccessfully`)

### `[MCP-ADMIN-REVERSE-PROXY-X-FORWARDED-HOST]` Admin SSE endpoint prioritizes X-Forwarded-Host header over Request.Host in endpoint URI advertising.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L514`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L514) (`AdminEndpoint_SseHandshake_UsesXForwardedHost`)

### `[MCP-ADMIN-SKILL-E2E-PROVISIONING]` Admin automation templates and JSON-RPC tool calls successfully provision a blank-slate gateway instance end-to-end via HTTP /admin/message.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L176`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L176) (`EndToEnd_BlankSlateProvisioning_ConfiguresAllEntitiesViaAdminTools`)

### `[MCP-ADMIN-SKILL-FRONTMATTER]` mcg-admin skill frontmatter is valid YAML, specifies name, description starting with 'Use when...', and length is under 1024 characters
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L21`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L21) (`Skill_Frontmatter_IsValidAndWithinCharacterLimit`)

### `[MCP-ADMIN-SKILL-MIRROR]` mcg-admin skill files and templates are identically mirrored between skills/ and .agents/skills/ directories
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L147`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L147) (`Skill_MirroredInAgentsDirectory`)

### `[MCP-ADMIN-SKILL-TEMPLATES]` All mcg-admin scaffold templates exist, are non-empty, and contain valid JSON or scripts for Authentik, Keycloak, Entra, ActiveDirectory, Cloudflare, Vault, Embeddings, Docker, and shell automation
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L103`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L103) (`Templates_AllExistAndAreValidJsonOrScripts`)

### `[MCP-ADMIN-SKILL-WORKFLOW]` mcg-admin skill contains all 7 administration phases including diagnostics, secrets, auth providers, RBAC/group mappings, settings/embeddings, servers/clients, and live tool verification
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L47`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L47) (`Skill_ContainsAllRequiredPhasesAndProviderCookbooks`)

### `[MCP-ADMIN-SSE-ZOD-COMPLIANT]` Admin endpoint direct POST response does not serialize null error or _meta fields.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L487`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L487) (`AdminEndpoint_DirectPost_SerializesResponseWithoutNullErrorOrMeta`)

### `[MCP-ADMIN-TOOL-AUDIT-LOG]` AdminMcpServer tool calls record audit log entries with caller and tool name.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L270`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L270) (`CallToolAsync_RecordsAuditLog`)

### `[MCP-ADMIN-TOOL-MANAGE-APPKEYS]` AdminMcpServer executes manage_appkeys create, list, limits, and revoke actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L287`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L287) (`CallToolAsync_ManageAppKeys_Lifecycle`)

### `[MCP-ADMIN-TOOL-MANAGE-CLIENTS]` AdminMcpServer executes manage_clients register, list, and delete actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L334`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L334) (`CallToolAsync_ManageClients_Lifecycle`)

### `[MCP-ADMIN-TOOL-MANAGE-CUSTOM-FILES]` AdminMcpServer executes manage_custom_files save, get, list, and delete actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L508`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L508) (`CallToolAsync_ManageCustomFiles_Lifecycle`)

### `[MCP-ADMIN-TOOL-MANAGE-GROUP-MAPPINGS]` AdminMcpServer executes manage_group_mappings save, list, and delete actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L406`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L406) (`CallToolAsync_ManageGroupMappings_Lifecycle`)

### `[MCP-ADMIN-TOOL-MANAGE-POLICIES]` AdminMcpServer executes manage_policies save, list, and delete actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L372`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L372) (`CallToolAsync_ManagePolicies_Lifecycle`)

### `[MCP-ADMIN-TOOL-MANAGE-PROVIDERS]` AdminMcpServer executes manage_providers list, save_secret, and save_auth actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L439`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L439) (`CallToolAsync_ManageProviders_Lifecycle`)

### `[MCP-ADMIN-TOOL-MANAGE-SERVERS]` AdminMcpServer executes manage_servers list, get, create, update, toggle, and delete actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L218`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L218) (`CallToolAsync_ManageServers_ListAndCreate`)

### `[MCP-ADMIN-TOOL-MANAGE-SETTINGS]` AdminMcpServer executes manage_settings get and update actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L482`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L482) (`CallToolAsync_ManageSettings_Lifecycle`)

### `[MCP-ADMIN-TOOL-MANAGE-SYSTEM]` AdminMcpServer executes manage_system diagnostics, get_logs, clear_logs, and query_audit actions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L562`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L562) (`CallToolAsync_ManageSystem_Lifecycle`)

### `[MCP-ADMIN-TOOLS-LIST-COUNT]` AdminMcpServer tools/list returns all 10 consolidated tools with complete JSON schemas.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L151`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L151) (`ListToolsAsync_ReturnsTenConsolidatedTools`)

### `[MCP-ADMIN-ZOD-STRICT-RESPONSE]` JsonRpcResponse serialization omits null result, error, and _meta fields completely to satisfy Zod strict validation.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L452`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L452) (`JsonRpcResponse_Serialization_OmitsNullFieldsCompletely`)

### `[MCP-COLDSTART-01]` Full cold-start cycle: SessionManager cache seeded -> search_tools -> execute_tool dispatches to downstream mock server and returns output.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L130`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L130) (`ColdStartCycle_SeedsRoutingTable_AndDispatchesExecuteToolDownstream`)

### `[MCP-DOWNSTREAM-INIT-DIAGNOSTICS]` Initializes downstream MCP backends with detailed diagnostic logging.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L317`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L317) (`TestInitializationDiagnostics`)

### `[MCP-ERROR-ACTIONABLE-SUGGESTION-CATEGORIES]` ToolErrorFormatter categorizes error messages and produces domain-specific remediation guidance.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L42) (`GetActionableSuggestion_ReturnsExpectedCategory`)

### `[MCP-ERROR-FORMAT-JSONRPC-REMEDIATION]` ToolErrorFormatter attaches actionable suggestions and remediation resource URIs to JSON-RPC error payloads.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L7) (`TransformError_FormatsJsonRpcErrorWithRemediation`)

### `[MCP-ERROR-FORMAT-UNHANDLED-EXCEPTION]` ToolErrorFormatter converts unhandled exceptions to standardized JSON-RPC error format with suggestions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L27`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L27) (`TransformException_FormatsExceptionWithRemediation`)

### `[MCP-EXEC-TOOL-ENFORCE-AUTH-POLICIES]` Meta-mode execute_tool strictly enforces target tool authorization policies
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L567`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L567) (`Pairwise_MetaMode_ExecuteTool_EnforcesTargetAuthorization`)

### `[MCP-EXEC-TOOL-ENFORCE-CATEGORY-SCOPES]` Router meta-mode execute_tool validates and enforces category scopes on target tool calls
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L428`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L428) (`ClientSession_ExecuteTool_EnforcesCategoryScopeOnInnerTarget`)

### `[MCP-FILES-DIR-HELPER-INIT]` CustomFilesDirectoryHelper initializes and creates required directories on startup.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L1069`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L1069) (`CustomFilesDirectoryHelper_CreatesDirectoriesCorrectly`)

### `[MCP-JSON-REWRITE-ADVERSARIAL-EDGE-CASES]` JsonNode request rewrite handles adversarial mixed arrays, block comments, and malformed JSON.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L572`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L572) (`JsonNode_Rewrite_HandlesAdversarialEdgeCases`)

### `[MCP-JSON-REWRITE-BATCH-COMMENTS-COMMAS]` RewriteRequestJson accurately parses JSON batches, comments, and trailing commas using System.Text.Json JsonNode.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L191) (`JsonNode_Rewrite_HandlesBatchCommentsAndCommas`)

### `[MCP-JSONRPC-CONVERTER-MINIMAL-VARIANTS]` JsonRpcMessageConverter deserializes edge-case minimal/null JSON-RPC variants without stack overflows.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L684`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L684) (`PlainJsonRpcMessages_DoNotCauseStackOverflow_PolymorphicVariants`)

### `[MCP-JSONRPC-CONVERTER-PRIORITIZE-RESPONSE]` JsonRpcMessageConverter prioritizes result/error properties over method property in polymorphic response parsing.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L336`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L336) (`SendRequestAsync_Succeeds_When_Response_Has_Method_Property`)

### `[MCP-JSONRPC-DESERIALIZE-PLAIN-NO-OVERFLOW]` Deserializing plain JsonRpcMessage does not cause recursive converter invocation or stack overflow.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L287`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L287) (`Deserializing_Plain_JsonRpcMessage_Does_Not_Cause_StackOverflow`)

### `[MCP-JSONRPC-POLYMORPHIC-DESERIALIZATION]` Polymorphic JSON-RPC message deserializer accurately instantiates request, response, and notification subclasses.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L259`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L259) (`PolymorphicDeserialization_Correctly_Deserializes_JsonRpcMessage_Subclasses`)

### `[MCP-JSONRPC-SERIALIZE-PLAIN-NO-OVERFLOW]` Serializing plain JsonRpcMessage does not cause recursive converter invocation or stack overflow.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L303`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L303) (`Serializing_Plain_JsonRpcMessage_Does_Not_Cause_StackOverflow`)

### `[MCP-MOCK-JSONRPC-FLOW]` MockDownstreamMcpServer handles initialize, initialized, tools/list, and tools/call JSON-RPC 2.0 protocol cycles.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L11) (`MockDownstreamMcpServer_HandlesStandardJsonRpcFlow`)

### `[MCP-MOCK-RESOURCES-PROMPTS-SUPPORT]` MockDownstreamMcpServer handles resources/list and prompts/list MCP protocol methods.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportResilienceTests.cs#L123`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L123) (`MockDownstreamMcpServer_HandlesResourcesAndPrompts`)

### `[MCP-RESILIENT-01]` Prefix-based resilient routing: execute_tool called with unregistered but prefixed tool name dynamically resolves server and executes.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L294`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L294) (`PrefixBasedResilientRouting_DynamicallyRegistersAndDispatchesTool`)

### `[MCP-SESSION-BUILTIN-PROMPTS]` ClientSession lists built-in diagnostic and routing prompts.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L115`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L115) (`ListPromptsAsync_ReturnsBuiltinDiagnosticPrompts`)

### `[MCP-SESSION-BUILTIN-RESOURCES]` ClientSession lists built-in resources including router://status.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L99`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L99) (`ListResourcesAsync_ReturnsBuiltinRouterStatusResource`)

### `[MCP-SESSION-CALL-SEARCH-TOOLS]` ClientSession calls built-in search_tools and returns structured search results.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L132) (`CallToolAsync_SearchTools_ExecutesSuccessfullyWithStructuredContent`)

### `[MCP-SESSION-ERROR-TRANSFORM-CANCEL-SAMPLING]` Translates backend error codes, handles cancellation tokens, and executes sampling requests.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L880`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L880) (`ErrorTransformation_Cancellation_And_Sampling_Works_Correctly`)

### `[MCP-SESSION-GET-PROMPT-DIAGNOSE]` ClientSession gets router__diagnose_failure prompt and returns diagnostic prompt instructions.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L176`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L176) (`GetPromptAsync_DiagnoseFailure_ReturnsDiagnosticInstructions`)

### `[MCP-SESSION-MANAGER-PER-SERVER-CACHE]` SessionManager caches and isolates connections per downstream backend server.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L1084`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L1084) (`SessionManager_PerServerCache_WorksCorrectly`)

### `[MCP-SESSION-METAMODE-TOOLS]` ClientSession exposes meta-mode tools search_tools and execute_tool in meta mode.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L80) (`ListToolsAsync_WhenInMetaMode_ExposesSearchAndExecuteTools`)

### `[MCP-SESSION-READ-ROUTER-STATUS]` ClientSession reads router://status resource and returns online gateway metadata.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L153`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L153) (`ReadResourceAsync_RouterStatus_ReturnsOnlineStatusPayload`)

### `[MCP-SESSION-REQUEST-CANCELLATION]` ClientSession registers and triggers request cancellation for active cancellation tokens.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L198`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L198) (`RegisterRequestCancellation_And_CancelRequest_CancelsActiveToken`)

### `[MCP-SESSION-UNREGISTERED-RESPONSE]` ClientSession returns false when handling client response for unregistered request ID.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientSessionTests.cs#L214`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L214) (`TryHandleClientResponse_ReturnsFalse_WhenNoPendingRequest`)

### `[MCP-WIRE-JSONRPC-SPEC]` MockDownstreamMcpServer treats messages with omitted id as notifications returning 202 Accepted with empty body.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L98) (`MockDownstreamMcpServer_TreatsOmittedIdAsNotification_ReturningAccepted`)

### `[MCP-WIRE-PROTOCOL-MATRIX]` MockDownstreamMcpServer dynamically negotiates requested protocol version.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L80) (`MockDownstreamMcpServer_DynamicallyNegotiatesProtocolVersion`)

### `[UI-104]` renders resource tester with servers and resources
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/ResourceTesterCard.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ResourceTesterCard.test.tsx#L1) (`renders resource tester with servers and resources`)

### `[UI-106]` renders connected server details with badges and triggers actions
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/ServerCard.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L1) (`renders connected server details with badges and triggers actions`)

### `[UI-107]` renders prompt dropdown and filters by selected server
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/PromptTesterCard.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PromptTesterCard.test.tsx#L1) (`renders prompt dropdown and filters by selected server`)

### `[UI-112]` renders nothing when isAddEditOpen is false
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L1) (`renders nothing when isAddEditOpen is false`)

### `[UI-121]` should open Add Server modal and switch secret provider types
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/server-management.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/server-management.spec.ts#L1) (`should open Add Server modal and switch secret provider types`)

### `[UI-126]` should open Server Inspect Modal if servers are present on dashboard
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/server-inspector.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/server-inspector.spec.ts#L1) (`should open Server Inspect Modal if servers are present on dashboard`)

### `[UI-SERVERS-ALIAS-MANAGEMENT]` renders server alias badge alongside server id when configured
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Frontend Vitest] [`frontend/src/test/components/ServerCard.test.tsx#L145`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L145) (`renders server alias badge alongside server id when configured (UI-SERVERS-ALIAS-MANAGEMENT)`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerModal.test.tsx#L186`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L186) (`renders alias input, validates characters, and submits alias (UI-SERVERS-ALIAS-MANAGEMENT)`)

### `[API-PIPELINE-GET-AUDIT]` GET /api/audit returns audit log records with 200 OK.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L393`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L393) (`Pipeline_GET_Audit_Returns200`)

### `[API-PIPELINE-GET-LOGS]` GET /api/logs returns system log records with 200 OK.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L420`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L420) (`Pipeline_GET_Logs_Returns200`)

### `[AUTH-107]` RegisterClient successfully handles DCR requests when open DCR is enabled.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L36`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L36) (`RegisterClient_CreatesApplicationAndReturnsOk`)

### `[AUTH-109]` RegisterClient uses IOAuthClientRepository when IOpenIddictApplicationManager is null.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L94`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L94) (`RegisterClient_UsesOAuthClientRepository_WhenApplicationManagerNull`)
  - [Frontend Vitest] [`frontend/src/test/pages/ConsentView.test.tsx#L16`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/pages/ConsentView.test.tsx#L16) (`renders client name from query string and sets form action and hidden inputs`)
  - [Playwright E2E] [`frontend/e2e/oauth-consent-flow.spec.ts#L5`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/oauth-consent-flow.spec.ts#L5) (`should render interactive OAuth consent screen and display requesting client name`)

### `[AUTH-113]` RegisterClient supports public clients with PKCE (token_endpoint_auth_method: none) and omits client secret.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L351`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L351) (`RegisterClient_PublicClient_SucceedsWithoutSecret`)

### `[AUTH-115]` RegisterClient dynamically binds requested scopes to OpenIddict application descriptor permissions.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L435`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L435) (`RegisterClient_DynamicScopes_AddedToPermissions`)

### `[MCP-ADMIN-TEST-TOOL-CALL-SECRET-RESOLUTION]` AdminMcpServer test_tool_call resolves server secrets via injected ISecretRetriever.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L765`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L765) (`TestToolCall_ResolvesSecretsViaInjectedSecretRetriever`)

### `[SEC-02]` VaultSecretRetriever dynamically loads, applies, and reloads Vault configurations from database repository.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (29):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L294`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L294) (`VaultSecretRetriever_DynamicallyLoadsAndAppliesDbConfig_WithReload`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L79`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L79) (`GetSecretProviders_ReturnsOkWithList`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L121`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L121) (`SaveSecretProvider_SavesSuccessfully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L257`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L257) (`TestVaultConnection_ValidatesInputAndHandlesFailureGracefully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L349`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L349) (`SaveSecretProvider_HttpUrl_AllowedForLocalhost`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L373`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L373) (`SaveSecretProvider_HttpUrl_AllowedForSimpleHost`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L8) (`GetSecretForProviderAsync_ReturnsNull_WhenProviderIsNone`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L26`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L26) (`GetSecretForProviderAsync_RoutesToTargetProvider_AndCachesValue`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L48`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L48) (`GetSecretForProviderAsync_MatchesVaultAliasNames`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L11) (`ProviderName_ReturnsHashiCorpVault`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L36`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L36) (`EnsureVaultClientAsync_ReturnsNull_WhenCredentialsMissing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L56`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L56) (`EnsureVaultClientAsync_CreatesClient_WhenValidConfig`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L78`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L78) (`GetSecretAsync_ReturnsCachedValue_WhenPresent`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L92`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L92) (`GetSecretAsync_ReturnsNull_WhenClientIsNull`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L482`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L482) (`ConnectAndInitializeBackendAsync_WithVaultServer_ResolvesRetrieverFromRootServices_WhenHttpContextIsNull`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L344`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L344) (`StdioTransport_ShouldPassSecretViaEnvironmentVariables_AndNotCommandLine`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L429`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L429) (`StdioTransport_ShouldSanitizeAndMaskSecretsInLogs`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L129`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L129) (`VaultUserSecretStore_SaveSecretAsync_WritesToVaultKv2`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L164`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L164) (`VaultUserSecretStore_DeleteSecretAsync_DeletesFromVaultKv2`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L187`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L187) (`VaultUserSecretStore_GetServerIdsAsync_ListsPathsFromVaultKv2`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L216`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L216) (`VaultUserSecretStore_Resolves_Enterprise_Path_Template_And_Discrete_KVs`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L375`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L375) (`Pipeline_GET_Providers_Secret_Returns200`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L37`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L37) (`GetSecretAsync_MintsTokenViaTokenExchange_AndCachesResponse`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L190`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L190) (`CompositeSecretRetriever_RoutesOboAndPocketIdAliases_ToTokenExchangeRetriever`)
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L41`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L41) (`successfully loads auth and secret providers`)
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L131`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L131) (`saves secret provider preserving Vault token and mount path`)
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L170`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L170) (`saves Windows Registry and Environment secret providers correctly`)
  - [Frontend Vitest] [`frontend/src/test/stores/useProviderStore.test.ts#L205`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L205) (`handles secret provider save error with toast and throws`)
  - [Playwright E2E] [`frontend/e2e/vault-approle-config-flow.spec.ts#L5`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/vault-approle-config-flow.spec.ts#L5) (`should configure Vault AppRole credentials and test connection in settings`)

### `[SEC-03]` EnvironmentSecretRetriever retrieves configured environment variable value.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (5):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SecretRetrieverTests.cs#L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecretRetrieverTests.cs#L5) (`EnvironmentSecretRetriever_ReturnsEnvVariable_WhenExists`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SecretRetrieverTests.cs#L25`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecretRetrieverTests.cs#L25) (`EnvironmentSecretRetriever_ReturnsNull_WhenVariableDoesNotExist`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L370`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L370) (`TrustedProxyHelper_AllowsXForwardedFor_WhenChainIsFullyTrusted`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L431`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L431) (`TrustedProxyHelper_ConfiguredProxyTrusted_CIDR`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnvironmentSecretRetrieverTests.cs#L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnvironmentSecretRetrieverTests.cs#L5) (`EnvironmentSecretRetriever_RetrievesSecret_FromEnvironmentVariables`)

### `[SEC-04]` WindowsRegistrySecretRetriever handles non-Windows platforms gracefully and returns null.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SecretRetrieverTests.cs#L34`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecretRetrieverTests.cs#L34) (`WindowsRegistrySecretRetriever_HandlesNonWindowsGracefully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L12) (`GetSecretAsync_ReturnsPlainString_WhenRegistryValueIsString`)
  - [Backend xUnit] [`ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L27`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L27) (`GetSecretAsync_DecryptsDpapiBytes_WhenRegistryValueIsByteArray`)
  - [Backend xUnit] [`ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L51`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L51) (`GetSecretAsync_ReturnsNull_WhenKeyNotFoundOrNull`)

### `[SEC-05]` renders RPC message stream with formatted JSON
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (6):**
  - [Frontend Vitest] [`frontend/src/test/components/LogsTerminalCard.test.tsx#L61`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LogsTerminalCard.test.tsx#L61) (`renders RPC message stream with formatted JSON`)
  - [Frontend Vitest] [`frontend/src/test/components/LogsTerminalCard.test.tsx#L81`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LogsTerminalCard.test.tsx#L81) (`toggles autoscroll and handles clear logs`)
  - [Frontend Vitest] [`frontend/src/test/components/LogsTerminalCard.test.tsx#L108`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LogsTerminalCard.test.tsx#L108) (`shows empty state when no logs match filter`)
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L56`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L56) (`renders img live preview when dashboardIcon is an image URL`)
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L88`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L88) (`updates dashboardIcon and live preview when a logo image file is uploaded`)
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L141`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L141) (`saves settings with the updated logo URL when form is submitted after upload`)

### `[SEC-ADMIN-AUDIT-REDACTION]` AdminMcpServer redacts sensitive secrets from argument payloads before recording audit logs.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L613`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L613) (`CallToolAsync_AuditLog_RedactsSensitivePayloadData`)

### `[SEC-APPKEY-SANITIZE-METADATA-GET]` AppKeys API returns sanitized key metadata without leaking plaintext tokens.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L259`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L259) (`GetAppKeys_ReturnsSanitizedKeys_ForAdminAndFiltered`)

### `[SEC-AUDIT-LOG-ADMIN-ACTION-RECORD]` AuditLogger records administrative configuration changes and security events to AdminAuditLogs table.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditLoggerTests.cs#L75`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditLoggerTests.cs#L75) (`LogAdminActionAsync_WritesEntryToDatabase`)

### `[SEC-AUDIT-LOG-INVOCATION-RECORD]` AuditLogger writes tool invocation audit records with actor attribution and duration to AuditLogs table.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditLoggerTests.cs#L60`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditLoggerTests.cs#L60) (`LogInvocationAsync_WritesEntryToDatabase`)

### `[SEC-AUDIT-LOGGER-ADMIN-PERSIST]` AuditLogger persists administrative actions directly into AdminAuditLogs database table.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditQueryApiTests.cs#L150`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L150) (`LogAdminActionAsync_WritesRowToAdminAuditLogs`)

### `[SEC-AUDIT-MAPPING-SAVE-ACTION]` PermissionsController records audit action when group mapping is saved.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditQueryApiTests.cs#L117`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L117) (`SaveMapping_WritesAuditAction_OnSuccess`)

### `[SEC-AUDIT-PER-REQUEST-ACTOR-ATTRIBUTION]` Audit logger attributes per-request actor credentials accurately across stateless calls.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L128`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L128) (`AuditLogger_RecordsPerRequestActor_NotHandshakeActor`)

### `[SEC-AUDIT-POLICY-SAVE-ACTION]` PermissionsController records audit action when access policy is saved.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditQueryApiTests.cs#L83`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L83) (`SavePolicy_WritesAuditAction_OnSuccess`)

### `[SEC-AUDIT-QUERY-FILTERED-ROWS]` Audit queries support filtering by user, server, and pagination while recording query access in audit log.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditQueryApiTests.cs#L60`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L60) (`AuditQuery_ReturnsFilteredRows_AndLogsAuditAction`)

### `[SEC-GATEWAY-ZERO-CONFIG-BOOT]` Gateway boots from a blank slate with zero master key environment variables, auto-generates .master.key, and serves health and admin endpoints.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L352`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L352) (`Gateway_BlankSlate_WithoutMasterKeyEnv_AutoGeneratesKeyFileAndBootsSuccessfully`)

### `[SEC-HTTP-SECRET-URL-FALLBACK]` HttpTransport falls back to URL and SecretItemKey when specific secret paths are unconfigured.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L160`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L160) (`HttpTransport_ResolveTokenAsync_Defaults_To_Url_And_ApiKey_When_Not_Configured`)

### `[SEC-KEY-PROVIDER-AUTOGEN]` EncryptionKeyProvider delegates to DbKeyHelper to auto-generate master key when unconfigured.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L42) (`GetDbEncryptionKey_AutoGenerates_WhenUnconfigured`)

### `[SEC-KEY-PROVIDER-CONFIG]` EncryptionKeyProvider returns configured DB_ENCRYPTION_KEY or MCG_SECRET.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L28`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L28) (`GetDbEncryptionKey_UsesConfig_WhenProvided`)

### `[SEC-KEY-PROVIDER-FALLBACK]` EncryptionKeyProvider falls back to DB_ENCRYPTION_KEY when MCG_SECRET is unconfigured.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L70`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L70) (`GetRouterSecret_FallsBackToDbEncryptionKey_WhenDbEncryptionKeyProvided`)

### `[SEC-KEY-PROVIDER-SECRET]` EncryptionKeyProvider returns configured MCG_SECRET.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L56`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L56) (`GetRouterSecret_UsesConfig_WhenProvided`)

### `[SEC-KEYFILE-AUTOGEN]` Blank-slate initialization auto-generates a 256-bit base64 master key and persists it to .master.key.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L63`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L63) (`ResolveDbEncryptionKey_AutoGeneratesAndPersistsKey_WhenBlankSlate`)

### `[SEC-KEYFILE-ENV-PRECEDENCE]` Explicit environment variables MCG_MASTER_KEY or MCG_SECRET take precedence over keyfiles.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L28`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L28) (`ResolveDbEncryptionKey_ReturnsConfiguredEnvKey_WhenPresent`)

### `[SEC-KEYFILE-FILE-OVER-KEYFILE]` Explicit file secrets take precedence over persistent .master.key files.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L123`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L123) (`ResolveDbEncryptionKey_FileSecretTakesPrecedenceOverKeyFile`)

### `[SEC-KEYFILE-FILE-SECRET]` File-based secrets configured via MCG_MASTER_KEY_FILE or standard Docker secrets paths are resolved.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L45`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L45) (`ResolveDbEncryptionKey_ReturnsFileSecret_WhenKeyFileSpecified`)

### `[SEC-KEYFILE-HIERARCHY-PRECEDENCE]` Explicit environment variables take precedence over file secrets and keyfiles.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L101`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L101) (`ResolveDbEncryptionKey_EnvVarTakesPrecedenceOverFileSecretAndKeyFile`)

### `[SEC-KEYFILE-RELOAD]` Existing .master.key file is loaded across gateway restarts without key mutation.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L83`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L83) (`ResolveDbEncryptionKey_LoadsExistingKeyFile_OnSubsequentBoot`)

### `[SEC-KEYSOURCE-DETECTION]` Correctly identifies KeySource origin for environment, file, and auto-generated keys.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L144`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L144) (`ResolveDbEncryptionKey_IdentifiesKeySourceAccurately`)

### `[SEC-KEYSOURCE-SETCACHEDKEY]` SetCachedKey sets in-memory encryption key and updates ActiveKeySource.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L314`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L314) (`SetCachedKey_UpdatesCachedKeyAndActiveKeySource`)

### `[SEC-LOG-PROVIDER-PRESERVE-PLAIN]` SanitizingLoggerProvider preserves non-sensitive log statements intact.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L43`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L43) (`SanitizingLoggerProvider_LeavesPlainMessagesUnchanged`)

### `[SEC-LOG-PROVIDER-REDACT-EXCEPTIONS]` SanitizingLoggerProvider sanitizes exception messages and stack traces to prevent credential leaks in error logs.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L76`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L76) (`SanitizingLoggerProvider_RedactsSecretsInExceptionMessageAndToString`)

### `[SEC-LOG-PROVIDER-REDACT-SECRETS]` SanitizingLoggerProvider automatically redacts Bearer tokens, API keys, and credentials in log message strings.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L8) (`SanitizingLoggerProvider_RedactsBearerTokensAndKeys`)

### `[SEC-MASTERKEY-ATOMIC-REENCRYPTION]` Atomically re-encrypts database credentials when setting a custom master key.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (5):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L142`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L142) (`SetMasterKey_AtomicallyReEncryptsDatabaseCredentials`)
  - [Backend xUnit] [`ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L241`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L241) (`SetMasterKey_RejectsWhenKeySourceIsExternalOrVault`)
  - [Backend xUnit] [`ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L259`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L259) (`AdminMcpServer_ManageSystem_SetMasterKey_ReencryptsCleanly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L338`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L338) (`SetMasterKey_RejectsInvalidOrShortKeys`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L481`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L481) (`Pipeline_POST_MasterKey_RejectsWhenExternalKeySource`)

### `[SEC-MASTERKEY-CONFIGURED-STATUS-BADGE]` Displays configured badge and rotate button when custom master key is configured.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTab.test.tsx#L192`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L192) (`renders configured badge and rotate key button when master key is Configured`)

### `[SEC-MASTERKEY-CUSTOM-MODAL-REENCRYPTION]` Validates master key inputs (length, match) and triggers atomic re-encryption.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Frontend Vitest] [`frontend/src/test/components/MasterKeyModal.test.tsx#L6`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MasterKeyModal.test.tsx#L6) (`validates key inputs and submits custom master key to callback`)
  - [Frontend Vitest] [`frontend/src/test/components/MasterKeyModal.test.tsx#L54`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MasterKeyModal.test.tsx#L54) (`generates a strong random master key when auto-generate button is clicked`)
  - [Frontend Vitest] [`frontend/src/test/components/MasterKeyModal.test.tsx#L85`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MasterKeyModal.test.tsx#L85) (`displays validation error when onSetMasterKey returns failure`)

### `[SEC-MASTERKEY-EXTERNAL-LOCKED-BADGE]` Displays locked badge when master key is externally managed via Vault or Environment.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTab.test.tsx#L149`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L149) (`renders locked badge when master key is managed externally`)

### `[SEC-MASTERKEY-UI-STATUS-BANNER]` Displays warning banner when keySource is AutoGenerated and opens custom master key modal.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTab.test.tsx#L115`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L115) (`renders AutoGenerated warning banner and opens MasterKeyModal`)

### `[SEC-PII-BASIC-COOKIE-QUERY-USERINFO]` PiiSanitizer redacts Basic auth credentials, session cookies, query access tokens, and URL userinfo credentials.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PiiSanitizerTests.cs#L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L53) (`PiiSanitizer_Redacts_Basic_ApiKey_Cookie_QueryToken_UrlUserInfo`)

### `[SEC-PII-BEARER-TOKEN]` PiiSanitizer redacts Bearer authentication tokens from payload strings.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PiiSanitizerTests.cs#L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L5) (`SanitizePayload_Redacts_Bearer_Tokens`)

### `[SEC-PII-CONNECTION-STRING-PASSWORD]` PiiSanitizer redacts database connection string passwords.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PiiSanitizerTests.cs#L29`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L29) (`SanitizePayload_Redacts_ConnectionString_Passwords`)

### `[SEC-PII-JSON-APIKEY-PASSWORD]` PiiSanitizer redacts apiKey, password, and secret properties in JSON payloads.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PiiSanitizerTests.cs#L16`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L16) (`SanitizePayload_Redacts_Api_Keys_And_Passwords`)

### `[SEC-PII-LOGBUFFER-IN-MEMORY]` LogBuffer sanitizes PII and credentials prior to buffering in memory.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PiiSanitizerTests.cs#L40`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L40) (`LogBuffer_Add_Sanitizes_PII_Payloads`)

### `[SEC-PROVIDER-MASK-SECRETS-GET]` ProvidersController GET endpoints mask sensitive tokens and passwords as asterisks.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L130`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L130) (`ProvidersController_GetEndpoints_RedactSensitiveSecrets`)

### `[SEC-PROVIDER-REDACT-AUDIT-PAYLOADS]` ProvidersController redacts sensitive secrets in administrative audit logs.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L185`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L185) (`ProvidersController_SaveEndpoints_RedactAuditLogPayloads`)

### `[SEC-SESSIONID-OPAQUE-NOT-BEARER]` Mcp-Session-Id header generates opaque UUIDs without leaking bearer tokens.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L1119`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L1119) (`Mcp_SessionId_IsOpaque_NotBearerToken`)

### `[SEC-VAULT-BOOTSTRAPPING]` Bootstraps master encryption key directly from HashiCorp Vault when VAULT_ADDR is configured.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L191) (`ResolveDbEncryptionKey_BootstrapsFromVault_WhenVaultConfigured`)

### `[SEC-VAULT-CUSTOM-MOUNT-PATH]` SseTransport resolves dynamic secrets from Vault using custom mounts, paths, and secret fields.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L134`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L134) (`SseTransport_ResolveTokenAsync_Uses_Custom_Path_Field_And_Mount`)

### `[SEC-VAULT-CUSTOM-PATH]` Bootstraps master key from Vault using custom mount path and secret key name.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L236`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L236) (`ResolveDbEncryptionKey_BootstrapsFromVault_WithCustomPathAndKeyName`)

### `[UI-105]` renders system logs and handles level filter
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/LogsTerminalCard.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LogsTerminalCard.test.tsx#L1) (`renders system logs and handles level filter`)

### `[TRANS-01]` HttpTransport formats X-API-Key header when downstream server AuthShape is 'x-api-key'.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L258`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L258) (`HttpTransport_Applies_XApiKey_AuthShape`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L280`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L280) (`HttpTransport_Applies_CustomHeader_AuthShape`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L303`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L303) (`HttpTransport_Applies_Slack_PerUser_Token_And_ForwardedUser`)
  - [Playwright E2E] [`frontend/e2e/full-ui-flow-http-direct.spec.ts#L8`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/full-ui-flow-http-direct.spec.ts#L8) (`should register HTTP server with Direct Key, verify status badge, and execute tool in Test Bench`)

### `[TRANS-02]` Register STDIO server with Env provider, verify connection card, and execute tool via Test Bench.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/full-ui-flow-stdio-env.spec.ts#L8`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/full-ui-flow-stdio-env.spec.ts#L8) (`should register STDIO server, verify card, and execute echo tool via Test Bench`)

### `[TRANS-04]` HTTP stateless transport correctly accumulates multi-line SSE streams and skips intermediate notification events
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/HttpTransportTests.cs#L72`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L72) (`SendRequestAsync_ParsesSseStreamWithIntermediateNotifications`)

### `[TRANS-05]` HTTP stateless transport reads entire multi-line and formatted JSON response bodies without premature truncation
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/HttpTransportTests.cs#L109`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L109) (`SendRequestAsync_ParsesFormattedMultiLineJsonPayload`)

### `[TRANS-06]` HTTP stateless transport joins multi-line SSE data fields into complete JSON payloads
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/HttpTransportTests.cs#L146`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L146) (`SendRequestAsync_ParsesMultiLineDataLinesInSse`)

### `[TRANS-AUTH-SHAPES-CUSTOM-HEADER]` SseTransport applies custom header names for proprietary target backend authentication.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L45`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L45) (`SseTransport_ApplyAuthAndCustomHeaders_Formats_CustomHeader`)

### `[TRANS-AUTH-SHAPES-HEADERS-JSON]` SseTransport parses and applies extra request headers from HeadersJson configuration.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L112`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L112) (`SseTransport_ApplyAuthAndCustomHeaders_Parses_HeadersJson`)

### `[TRANS-AUTH-SHAPES-QUERY-PARAM]` SseTransport appends authentication tokens as URL query parameters when query auth shape is configured.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L67`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L67) (`SseTransport_ApplyAuthAndCustomHeaders_Appends_QueryParameter`)

### `[TRANS-AUTH-SHAPES-STANDARD-HEADERS]` SseTransport formats standard authorization shapes (bearer, basic, raw, x-api-key) into HTTP headers.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L17`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L17) (`SseTransport_ApplyAuthAndCustomHeaders_Formats_Standard_Headers`)

### `[TRANS-BACKEND-MULTIPLEX-CONCURRENT-RPC]` BackendConnection multiplexes 100+ concurrent asynchronous polymorphic RPC requests without deadlocking.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L494`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L494) (`AsynchronousRouting_HighVolumeAndPolymorphic_DoesNotHang`)

### `[TRANS-DISCONNECT-PENDING-CLEANUP]` Cleans up and cancels pending requests upon backend transport disconnect.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L277`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L277) (`BackendDisconnectCleanup_ClearsPendingRequests`)

### `[TRANS-EXPLICIT-NULL-ID-ISOLATION]` Handles JSON-RPC requests with explicit null IDs and multiplexes upstream calls correctly.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L346`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L346) (`ConcurrentResponseIsolation_ExplicitNullId_Succeeds`)

### `[TRANS-HIGH-CONCURRENCY-ISOLATION]` Maintains strict response isolation under high concurrency with 100+ callers reusing identical RPC IDs.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L111`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L111) (`HighConcurrencyResponseIsolation_RepeatedIdsAcrossCallers`)

### `[TRANS-HTTP-AUTH-CUSTOM-HEADER]` HttpTransport formats custom header authentication for target servers.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L90`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L90) (`HttpTransport_ApplyAuthAndCustomHeaders_Formats_CustomHeader`)

### `[TRANS-HTTP-RESOLVE-STATIC-APIKEY]` HTTP stateless transport resolves static API keys when secret provider is None
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/HttpTransportTests.cs#L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L11) (`ResolveTokenAsync_ReturnsApiKey_WhenProviderNone`)

### `[TRANS-ISOLATION-SAME-ID-REVERSED-ORDER]` Multiplexes concurrent client calls sharing identical JSON-RPC IDs and routes reversed responses correctly.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L12) (`ConcurrentResponseIsolation_TwoCallersSameId_SucceedsWithReversedResponseOrder`)

### `[TRANS-MIXED-ID-TYPES-ISOLATION]` Handles mixed numeric, string, and null JSON-RPC IDs concurrently across backend transports.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L614`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L614) (`ConcurrentResponseIsolation_MixedNumericStringNullIds`)

### `[TRANS-NOTIFICATION-NO-RESPONSE-LISTENER]` Handles JSON-RPC notifications without registering pending response listeners.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L419`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L419) (`ConcurrentResponseIsolation_Notification_DoesNotExpectResponse`)

### `[TRANS-SSE-LOG-ENDPOINT-WAIT-EXCEPTION]` SSE transport logs exceptions gracefully when waiting for endpoint URL without throwing unhandled exceptions.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SseTransportTests.cs#L76`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L76) (`SendRequestAsync_HandlesEndpointWaitTimeoutGracefully`)

### `[TRANS-SSE-NOTIF-FORWARD-FIELDS-INTACT]` SSE backend notifications are forwarded to client sessions with all payload fields intact.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L419`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L419) (`SseBackend_Notification_IsForwardedToClient_WithAllFieldsIntact`)

### `[TRANS-SSE-RESOLVE-STATIC-APIKEY]` SSE transport resolves static plaintext API keys when provider is None
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SseTransportTests.cs#L13`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L13) (`ResolveTokenAsync_ReturnsApiKey_WhenProviderNone`)

### `[TRANS-SSE-STREAM-LIFECYCLE]` SSE transport correctly resolves relative endpoint URLs and ignores keep-alive SSE comments.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SseTransportTests.cs#L100`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L100) (`SseTransport_ResolvesRelativeEndpointUrl_AndProcessesKeepAliveComments`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SseTransportTests.cs#L143`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L143) (`SseTransport_MultiplexesResponse_CorrelatingUpstreamRequestId`)

### `[TRANS-STATELESS-CANCELLATION-ISOLATION]` Isolates cancellation tokens between concurrent stateless client requests.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L482`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L482) (`ClientSession_ConcurrentStatelessRequestIsolateCancellation`)

### `[TRANS-STDIO-DRAIN-BUFFER-EOF]` STDIO transport drains buffered stdout/stderr streams to EOF when process exits rapidly
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L472`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L472) (`StdioTransport_ShouldDrainReaderStreamsToEOF_WhenProcessExitsImmediately`)

### `[TRANS-STDIO-SPAWN-TOOL-CALL]` STDIO transport spawns subprocess, handles JSON-RPC initialization and executes tool calls
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L49`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L49) (`StdioTransport_ShouldInitializeAndCallToolSuccessfully`)

### `[TRANS-STDIO-STREAM-STDERR-LOGS]` STDIO transport streams subprocess stderr asynchronously to structured router diagnostic logs
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L170`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L170) (`StdioTransport_ShouldRouteStderrToLogs`)

### `[TRANS-STDIO-TERMINATE-CLEANLY]` STDIO transport terminates subprocess tree cleanly upon disposal or cancellation
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L242`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L242) (`StdioTransport_ShouldSupportCancellationAndProcessTreeTermination`)

### `[TRANS-STDIO-TOKENIZE-PRESERVE-QUOTES]` STDIO command-line tokenizer preserves quoted arguments and space escaping
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L327`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L327) (`StdioTransport_ParseCommandLine_Handles_Quotes_And_Spaces`)

### `[TRANS-TARGETED-CANCELLATION-ISOLATION]` Targeted cancellation does not cancel concurrent client sessions reusing identical RPC IDs.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L532`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L532) (`ClientSession_TargetedCancellation_DoesNotCancelOtherClientsReusingId`)

### `[TRANS-TIMEOUT-PENDING-CLEANUP]` SendRequestAsync times out cleanly and removes pending completion handlers without leaking memory.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L276`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L276) (`SendRequestAsync_TimesOutCleanly_AndDoesNotLeak`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L217`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L217) (`TimeoutAndCancellationCleanup_DoesNotLeavePendingRequests`)

### `[TRANS-VALIDATION-HTTP-ALLOWED-IPS]` ServerValidationHelper accepts valid HTTP/HTTPS endpoints allowed by IP security rules.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L38`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L38) (`IsValidServerUrl_Accepts_Valid_Http_Urls`)

### `[UI-01]` opens confirmation modal and resolves true when confirmed
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (61):**
  - [Frontend Vitest] [`frontend/src/test/stores/useConfirmStore.test.ts#L31`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useConfirmStore.test.ts#L31) (`opens confirmation modal and resolves true when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/useConfirmStore.test.ts#L58`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useConfirmStore.test.ts#L58) (`resolves false when cancelled`)
  - [Frontend Vitest] [`frontend/src/test/stores/useConfirmStore.test.ts#L76`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useConfirmStore.test.ts#L76) (`settles existing pending promise with false when a new confirmation is opened`)
  - [Frontend Vitest] [`frontend/src/test/components/DashboardView.test.tsx#L115`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/DashboardView.test.tsx#L115) (`renders empty state when no servers match search`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L26`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L26) (`renders CustomFileModal in create mode and displays visual builder tabs`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L41`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L41) (`allows adding and removing arguments in visual builder`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L68`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L68) (`allows adding and removing messages in visual builder`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L95`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L95) (`switches between Raw JSON Editor and Visual Prompt Builder with synchronization`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L177`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L177) (`changes file type to resources and adjusts extension`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L194`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L194) (`submits form and calls saveCustomFile`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L222`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L222) (`renders in edit mode when editingFileMeta is set`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L244`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L244) (`allows adding assistant messages, modifying argument required checkbox, and rendering empty arguments state`)
  - [Frontend Vitest] [`frontend/src/test/components/HeaderBranding.test.tsx#L37`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/HeaderBranding.test.tsx#L37) (`identifies FontAwesome class names and invalid inputs as non-image URLs`)
  - [Frontend Vitest] [`frontend/src/test/components/HeaderBranding.test.tsx#L54`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/HeaderBranding.test.tsx#L54) (`updates document.title and sets custom image favicon when icon is an image URL`)
  - [Frontend Vitest] [`frontend/src/test/components/HeaderBranding.test.tsx#L69`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/HeaderBranding.test.tsx#L69) (`sets default title and generated SVG favicon when branding is null or uses FontAwesome icon`)
  - [Frontend Vitest] [`frontend/src/test/components/HeaderBranding.test.tsx#L87`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/HeaderBranding.test.tsx#L87) (`renders img element with logo-icon logo-img class when branding.icon is an image endpoint`)
  - [Frontend Vitest] [`frontend/src/test/components/HeaderBranding.test.tsx#L115`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/HeaderBranding.test.tsx#L115) (`renders FontAwesome i element when branding.icon is a FontAwesome class`)
  - [Frontend Vitest] [`frontend/src/test/components/MappingModal.test.tsx#L27`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MappingModal.test.tsx#L27) (`renders create mapping form with empty inputs`)
  - [Frontend Vitest] [`frontend/src/test/components/MappingModal.test.tsx#L42`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MappingModal.test.tsx#L42) (`renders edit mapping form pre-filled with mapping data`)
  - [Frontend Vitest] [`frontend/src/test/components/MappingModal.test.tsx#L57`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MappingModal.test.tsx#L57) (`submits form with externalId and internalGroup`)
  - [Frontend Vitest] [`frontend/src/test/components/MappingModal.test.tsx#L82`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MappingModal.test.tsx#L82) (`closes modal on cancel click`)
  - [Frontend Vitest] [`frontend/src/test/components/Header.test.tsx#L38`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/Header.test.tsx#L38) (`renders admin badge and shield icon for full_admin users`)
  - [Frontend Vitest] [`frontend/src/test/components/Header.test.tsx#L64`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/Header.test.tsx#L64) (`renders standard user badge for non-admin users`)
  - [Frontend Vitest] [`frontend/src/test/components/Header.test.tsx#L90`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/Header.test.tsx#L90) (`does not render user status item when unauthenticated`)
  - [Frontend Vitest] [`frontend/src/test/components/Header.test.tsx#L110`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/Header.test.tsx#L110) (`displays gateway status and SSE endpoint`)
  - [Frontend Vitest] [`frontend/src/test/components/Header.test.tsx#L126`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/Header.test.tsx#L126) (`toggles light and dark theme on button click and updates document attribute`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsTabs.test.tsx#L49`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsTabs.test.tsx#L49) (`renders IdentityAuthTab and SecretProvidersTab inside ProvidersTab`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsTabs.test.tsx#L77`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsTabs.test.tsx#L77) (`renders CustomFilesTab and triggers modal open and delete`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsTabs.test.tsx#L110`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsTabs.test.tsx#L110) (`renders AccessControlTab with policies and mappings`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsTabs.test.tsx#L139`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsTabs.test.tsx#L139) (`renders BackupsTab`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsView.test.tsx#L144`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsView.test.tsx#L144) (`saves embedding settings and displays success feedback`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsView.test.tsx#L183`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsView.test.tsx#L183) (`saves Auth Provider configurations including Active Directory and OIDC header mappings`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsView.test.tsx#L240`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsView.test.tsx#L240) (`saves secret providers while preserving Vault config and secrets`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsView.test.tsx#L311`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsView.test.tsx#L311) (`renders custom files table with edit and delete actions`)
  - [Frontend Vitest] [`frontend/src/test/components/SettingsView.test.tsx#L354`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsView.test.tsx#L354) (`renders access policies and group mappings with CRUD actions`)
  - [Frontend Vitest] [`frontend/src/test/components/TestBenchView.test.tsx#L71`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/TestBenchView.test.tsx#L71) (`handles semantic search queries in SemanticRouterCard`)
  - [Frontend Vitest] [`frontend/src/test/components/TestBenchView.test.tsx#L99`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/TestBenchView.test.tsx#L99) (`executes tool and updates console`)
  - [Frontend Vitest] [`frontend/src/test/components/TestBenchView.test.tsx#L130`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/TestBenchView.test.tsx#L130) (`executes prompt get in prompt tester tab`)
  - [Frontend Vitest] [`frontend/src/test/components/TestBenchView.test.tsx#L164`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/TestBenchView.test.tsx#L164) (`executes resource read in resource inspector tab`)
  - [Frontend Vitest] [`frontend/src/test/components/SharedComponents.test.tsx#L26`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SharedComponents.test.tsx#L26) (`renders title, children, and handles close button click`)
  - [Frontend Vitest] [`frontend/src/test/components/SharedComponents.test.tsx#L50`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SharedComponents.test.tsx#L50) (`renders various statuses correctly with indicators`)
  - [Frontend Vitest] [`frontend/src/test/components/SharedComponents.test.tsx#L72`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SharedComponents.test.tsx#L72) (`returns null when totalItems is 0`)
  - [Frontend Vitest] [`frontend/src/test/components/SharedComponents.test.tsx#L91`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SharedComponents.test.tsx#L91) (`renders page info and navigation controls`)
  - [Frontend Vitest] [`frontend/src/test/components/SharedComponents.test.tsx#L130`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SharedComponents.test.tsx#L130) (`handles pageSize all`)
  - [Frontend Vitest] [`frontend/src/test/components/ConfirmModal.test.tsx#L32`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ConfirmModal.test.tsx#L32) (`renders title, message, and action buttons when open`)
  - [Frontend Vitest] [`frontend/src/test/components/ConfirmModal.test.tsx#L58`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ConfirmModal.test.tsx#L58) (`calls handleConfirm when confirm button clicked`)
  - [Frontend Vitest] [`frontend/src/test/components/ConfirmModal.test.tsx#L82`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ConfirmModal.test.tsx#L82) (`calls handleCancel when cancel button clicked`)
  - [Frontend Vitest] [`frontend/src/test/components/LayoutCentering.test.tsx#L19`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LayoutCentering.test.tsx#L19) (`renders top navigation bar with centered alignment in layout.css and App`)
  - [Frontend Vitest] [`frontend/src/test/components/LayoutCentering.test.tsx#L42`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LayoutCentering.test.tsx#L42) (`renders tester tabs with centered alignment in tester.css`)
  - [Frontend Vitest] [`frontend/src/test/components/LayoutCentering.test.tsx#L58`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LayoutCentering.test.tsx#L58) (`renders SettingsView sub-navigation bar with centered alignment`)
  - [Frontend Vitest] [`frontend/src/test/components/LayoutCentering.test.tsx#L74`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LayoutCentering.test.tsx#L74) (`renders AppKeysCard sub-navigation tabs with centered alignment for admin`)
  - [Frontend Vitest] [`frontend/src/test/components/LayoutCentering.test.tsx#L90`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LayoutCentering.test.tsx#L90) (`uses body::before and body::after pseudo-elements for ambient gradients and removes background-decor DOM nodes`)
  - [Frontend Vitest] [`frontend/src/test/components/LayoutCentering.test.tsx#L117`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LayoutCentering.test.tsx#L117) (`defines focus-visible outline indicators for interactive focus styling`)
  - [Frontend Vitest] [`frontend/src/test/api/typedApi.test.ts#L74`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/api/typedApi.test.ts#L74) (`calls client and appkey endpoints correctly`)
  - [Frontend Vitest] [`frontend/src/test/api/typedApi.test.ts#L125`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/api/typedApi.test.ts#L125) (`calls user quota endpoints correctly`)
  - [Frontend Vitest] [`frontend/src/test/api/typedApi.test.ts#L154`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/api/typedApi.test.ts#L154) (`calls policies and mappings endpoints correctly`)
  - [Frontend Vitest] [`frontend/src/test/api/typedApi.test.ts#L181`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/api/typedApi.test.ts#L181) (`calls settings, providers, custom files, approvals endpoints correctly`)
  - [Frontend Vitest] [`frontend/src/test/api/typedApi.test.ts#L237`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/api/typedApi.test.ts#L237) (`calls testbench tool, prompt, resource, log endpoints correctly`)
  - [Playwright E2E] [`frontend/e2e/prompts-resources-customfiles.spec.ts#L42`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/prompts-resources-customfiles.spec.ts#L42) (`should navigate to Custom Files and Prompts in Settings view`)
  - [Playwright E2E] [`frontend/e2e/dashboard.spec.ts#L23`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/dashboard.spec.ts#L23) (`should display aggregate statistics cards`)
  - [Playwright E2E] [`frontend/e2e/dashboard.spec.ts#L37`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/dashboard.spec.ts#L37) (`should filter servers using search input`)

### `[UI-02]` Inspect modal displays spinner loading state while querying server capabilities
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (6):**
  - [Frontend Vitest] [`frontend/src/test/components/ServerInspectModal.test.tsx#L61`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L61) (`renders loading state when inspectLoading is true`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerInspectModal.test.tsx#L79`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L79) (`renders tools tab with schema and handles tab switching`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerInspectModal.test.tsx#L116`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L116) (`renders resources tab items and handles search filtering`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerInspectModal.test.tsx#L144`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L144) (`renders prompts tab with arguments and empty state when filtered out`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerInspectModal.test.tsx#L166`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L166) (`renders empty states for tabs when data is empty`)
  - [Frontend Vitest] [`frontend/src/test/components/ServerInspectModal.test.tsx#L194`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L194) (`closes modal when close button is clicked`)

### `[UI-03]` Grouped server view renders category sections and supports collapsible groups
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Frontend Vitest] [`frontend/src/test/components/DashboardView.test.tsx#L63`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/DashboardView.test.tsx#L63) (`renders grouped server view by category and allows collapsing`)
  - [Frontend Vitest] [`frontend/src/test/components/DashboardView.test.tsx#L90`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/DashboardView.test.tsx#L90) (`renders grouped server view by status and type`)

### `[UI-04]` Tool selector filters available tools by selected backend server
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (7):**
  - [Frontend Vitest] [`frontend/src/test/components/ToolTesterCard.test.tsx#L77`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L77) (`filters tools by selected server and handles tool change`)
  - [Frontend Vitest] [`frontend/src/test/components/ToolTesterCard.test.tsx#L106`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L106) (`filters custom tools with no namespace prefix when selectedServer is custom`)
  - [Frontend Vitest] [`frontend/src/test/components/ToolTesterCard.test.tsx#L131`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L131) (`renders dynamic fields for boolean, number, string, array, and object types`)
  - [Frontend Vitest] [`frontend/src/test/components/ToolTesterCard.test.tsx#L178`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L178) (`renders empty state when selected tool takes no arguments`)
  - [Frontend Vitest] [`frontend/src/test/components/ToolTesterCard.test.tsx#L203`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L203) (`switches to raw JSON tab and handles raw JSON editing`)
  - [Frontend Vitest] [`frontend/src/test/components/ToolTesterCard.test.tsx#L242`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L242) (`handles form submission`)
  - [Playwright E2E] [`frontend/e2e/prompts-resources-customfiles.spec.ts#L5`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/prompts-resources-customfiles.spec.ts#L5) (`should interact with Prompt Tester and Resource Tester cards in Test Bench`)

### `[UI-05]` Router allows customized branding parameters (DashboardTitle, DashboardIcon) to be saved and retrieved via the API.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L253`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L253) (`Pipeline_Settings_Branding_ReadWrite`)
  - [Frontend Vitest] [`frontend/src/test/components/HeaderBranding.test.tsx#L7`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/HeaderBranding.test.tsx#L7) (`identifies image URLs and paths accurately`)
  - [Frontend Vitest] [`frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L6`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTabLogoUpload.test.tsx#L6) (`renders branding label and FontAwesome icon preview when icon is a CSS class`)

### `[UI-06]` Router supports uploading and retrieving custom branding logo images via dedicated endpoints.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L447`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L447) (`Branding_Logo_Upload_And_Retrieval_Works`)

### `[UI-07]` Audits desktop viewport layout for zero horizontal overflow and high UX score.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Playwright E2E] [`frontend/e2e/layout-inspector.spec.ts#L38`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/layout-inspector.spec.ts#L38) (`should pass layout audit on desktop 1080p viewport`)
  - [Playwright E2E] [`frontend/e2e/layout-inspector.spec.ts#L64`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/layout-inspector.spec.ts#L64) (`should pass layout audit on Samsung Galaxy S25+ mobile viewport`)

### `[UI-102]` Dashboard renders stats card, connected server list, and setup instructions
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/DashboardView.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/DashboardView.test.tsx#L1) (`renders stats card, server list, and client setup guide`)

### `[UI-103]` Interactive tool tester renders server and tool selection dropdowns
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/ToolTesterCard.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L1) (`renders initial server and tool selection options`)

### `[UI-108]` renders nothing when isMappingModalOpen is false
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/MappingModal.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MappingModal.test.tsx#L1) (`renders nothing when isMappingModalOpen is false`)

### `[UI-109]` Renders ClientSetupGuide below the user credentials card.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (2):**
  - [Frontend Vitest] [`frontend/src/test/pages/MyMcpServers.test.tsx#L102`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/pages/MyMcpServers.test.tsx#L102) (`renders client setup guide below credentials card`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientSetupGuide.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientSetupGuide.test.tsx#L1) (`renders default standard mcpServers configuration with meta mode`)

### `[UI-110]` renders title, MCG badge, subtitle, and version badge
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/Header.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/Header.test.tsx#L1) (`renders title, MCG badge, subtitle, and version badge`)

### `[UI-111]` renders GeneralTab and triggers save
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/SettingsTabs.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsTabs.test.tsx#L1) (`renders GeneralTab and triggers save`)

### `[UI-113]` renders tab navigation and switches active subviews
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/SettingsView.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsView.test.tsx#L1) (`renders tab navigation and switches active subviews`)

### `[UI-115]` renders test bench cards and switches tabs
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/TestBenchView.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/TestBenchView.test.tsx#L1) (`renders test bench cards and switches tabs`)

### `[UI-116]` Modal remains hidden when isInspectOpen is false
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/ServerInspectModal.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L1) (`renders nothing when isInspectOpen is false`)

### `[UI-117]` returns null when isOpen is false
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/components/SharedComponents.test.tsx#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SharedComponents.test.tsx#L1) (`returns null when isOpen is false`)

### `[UI-119]` calls server endpoints correctly
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/api/typedApi.test.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/api/typedApi.test.ts#L1) (`calls server endpoints correctly`)

### `[UI-122]` should navigate to Settings view and configure vector embedding options
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/settings.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/settings.spec.ts#L1) (`should navigate to Settings view and configure vector embedding options`)

### `[UI-124]` Renders main dashboard navigation tabs and layout headers
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/dashboard.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/dashboard.spec.ts#L1) (`should render the dashboard layout and header components`)

### `[UI-128]` should navigate to Test Bench view and render tester cards
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Playwright E2E] [`frontend/e2e/testbench.spec.ts#L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/testbench.spec.ts#L1) (`should navigate to Test Bench view and render tester cards`)

### `[UI-30]` Renders client registration form with inputs for name, client type, redirect URIs, grant types, scopes, and expiration.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (3):**
  - [Frontend Vitest] [`frontend/src/test/components/ClientModal.test.tsx#L27`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientModal.test.tsx#L27) (`renders client registration form with rich OAuth fields and cancel button`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientModal.test.tsx#L55`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientModal.test.tsx#L55) (`submits registration form with parsed scopes array and OAuth metadata`)
  - [Frontend Vitest] [`frontend/src/test/components/ClientModal.test.tsx#L94`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientModal.test.tsx#L94) (`renders one-time secret display result card with copy buttons when createdClientResult is populated`)

### `[UI-32]` Registers OAuth client with extended metadata (redirect URIs, grant types, client type, expiration) and captures one-time credentials.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Positive Feature Capability
* **Verification Proofs (1):**
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L76`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L76) (`creates client with one-time secret result and refreshes list`)

---

## 3. Boundary & Guardrail Invariants ("What the Application DOES NOT DO")

> [!IMPORTANT]
> The following guardrails define strict security boundaries, fail-closed fault invariants, and forbidden application states.

### `[AUTH-ADMIN-POLICY-REJECT-REGULAR]` AdminPolicy rejects principal with unconfigured regular role without Admin SID or Admin Group
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L116`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L116) (`AdminPolicy_Denies_StandardRole_WithoutAdminSidOrGroup`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminPolicySidOnlyTests.cs#L16`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicySidOnlyTests.cs#L16) (`AdminPolicy_Denies_StandardRole_Without_AdminSid`)

### `[AUTH-ADMIN-REJECT-NONADMIN]` SecurityValidationHelper rejects non-admin groups and guest identities
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L260`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L260) (`SecurityValidationHelper_IsAdmin_RejectsNonAdminGroups`)

### `[AUTH-PERSONAL-APPKEY-CREATE]` Non-admin users can create personal App Keys up to quota
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (9):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L191) (`CreateAppKey_NonAdmin_CreatesPersonalKey_UpToDefaultQuota`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L421`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L421) (`creates category-scoped key, captures one-time plaintext key, and refreshes`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeyModal.test.tsx#L19`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeyModal.test.tsx#L19) (`renders nothing when isCreateModalOpen is false`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeyModal.test.tsx#L61`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeyModal.test.tsx#L61) (`locks key type to personal key for non-admin and shows quota feedback`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeyModal.test.tsx#L115`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeyModal.test.tsx#L115) (`handles scope serialization for server scope and target username for admin`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeyModal.test.tsx#L154`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeyModal.test.tsx#L154) (`handles scope serialization for category scope and expiration days`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeyModal.test.tsx#L192`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeyModal.test.tsx#L192) (`disables submit button when quota limit is reached`)
  - [Frontend Vitest] [`frontend/src/test/components/AppKeyModal.test.tsx#L217`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/AppKeyModal.test.tsx#L217) (`displays one-time secret result and copies plaintext key to clipboard`)
  - [Playwright E2E] [`frontend/e2e/personal-appkeys-and-quotas.spec.ts#L33`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/personal-appkeys-and-quotas.spec.ts#L33) (`Non-Admin Context: mints personal key, views snippet, and revokes key`)

### `[GUARD-LDAP-FAIL-CLOSED]` LdapActiveDirectoryService fails closed with SecurityException when LDAP connection throws an exception.
* **Category:** `AUTH` (Authentication, RBAC & Identity)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L132) (`ResolveUserSidsAsync_ThrowsSecurityException_OnConnectionFailure`)

### `[CORE-GATEWAY-METADATA-UNSUPPORTED-VERSIONS]` IsSupportedProtocolVersion rejects unrecognized protocol versions.
* **Category:** `CORE` (CORE)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GatewayMetadataTests.cs#L32`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L32) (`IsSupportedProtocolVersion_ReturnsFalse_ForUnknownVersions`)

### `[SEC-DB-ENCRYPTION-KEY-AUTOGEN-FAIL]` ResolveDbEncryptionKey wraps file persistence errors in InvalidOperationException
* **Category:** `DB` (Multi-Database Persistence & Migrations)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L334`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L334) (`ResolveDbEncryptionKey_ThrowsInvalidOperationException_WhenAutoGenerationFails`)

### `[AUTH-EXTERNAL-IDP-DENIES-ANONYMOUS-LOOPBACK]` When an external IDP is configured, anonymous loopback requests do not bypass authentication.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L224`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L224) (`AdminPolicy_ExternalIdpConfigured_LoopbackIp_RequiresCredentials`)

### `[AUTH-STANDALONE-ADMINPOLICY-EXTERNAL-DENY]` AdminPolicy rejects unauthenticated requests from non-whitelisted external IPs in standalone mode.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L200`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L200) (`AdminPolicy_StandaloneMode_ExternalUntrustedIp_FailsAdminPolicy`)

### `[AUTH-STANDALONE-EXTERNAL-DENY]` Standalone mode denies admin access to non-whitelisted external IPs without an Admin AppKey.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L57`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L57) (`IsAdmin_StandaloneMode_UntrustedIp_ReturnsFalse`)

### `[GUARD-01]` handles policy save failure with error toast
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (2):**
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L86`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L86) (`handles policy save failure with error toast`)
  - [Frontend Vitest] [`frontend/src/test/components/PolicyModal.test.tsx#L60`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L60) (`submits form with constructed payload for DENY policy`)

### `[GUARD-02]` SSE transport fails closed with SecurityException when secret provider resolution fails
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (12):**
  - [Backend xUnit] [`ModelContextGateway.Tests/SseTransportTests.cs#L34`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L34) (`ResolveTokenAsync_ThrowsSecurityException_WhenSecretProviderFails`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SseTransportTests.cs#L55`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L55) (`ResolveTokenAsync_ThrowsInvalidOperationException_WhenNoRetrieverRegistered`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L37`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L37) (`ResolveUserSidsAsync_ThrowsInvalidOperation_WhenDbConfigSpecifiesPlaintextLdap`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L78`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceIntegrationTests.cs#L78) (`ResolveUserSidsAsync_FailsClosedWithSecurityException_OnUnreachableServer`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DbKeyHelperTests.cs#L283`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L283) (`ResolveDbEncryptionKey_ThrowsInvalidOperationException_WhenVaultFails`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L218`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L218) (`HttpTransport_SendRequestAsync_Throws_When_Impersonation_Missing_WindowsIdentity`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L188`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L188) (`LdapService_ThrowsInvalidOperation_WhenUseSslFalse`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L208`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L208) (`LdapService_ThrowsSecurityException_OnBindFailure`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L394`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L394) (`StdioTransport_ShouldFailClosed_WhenSecretResolutionFails`)
  - [Backend xUnit] [`ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L64`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L64) (`ResolveUserSidsAsync_ThrowsInvalidOperation_WhenPlaintextLdapConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L61`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L61) (`EnsureVaultClientAsync_ReturnsNull_WhenVaultProviderDisabledInRepo`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L113`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L113) (`GetSecretAsync_ThrowsSecurityException_OnVaultException`)

### `[GUARD-03]` CompositeSecretRetriever throws InvalidOperationException when an unregistered secret provider is requested.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (13):**
  - [Backend xUnit] [`ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L17`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L17) (`GetSecretForProviderAsync_ThrowsInvalidOperationException_WhenProviderNotRegistered`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L19`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L19) (`EnsureVaultClientAsync_ThrowsArgumentException_WhenAddressInvalidScheme`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L104`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultSecretRetrieverTests.cs#L104) (`GetSecretAsync_UsesCustomVaultClientFactory`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L184`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L184) (`SseTransport_ResolveTokenAsync_FailsClosed_WhenVaultResolvesNull`)
  - [Backend xUnit] [`ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L66`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/WindowsRegistrySecretRetrieverTests.cs#L66) (`GetSecretAsync_HandlesExceptionGracefully_ReturnsNull`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L104`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L104) (`StdioTransport_ShouldThrowSecurityExceptionForUnsafeExecutable`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L126`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L126) (`StdioTransport_ShouldThrowSecurityExceptionForShellExecutable`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L148`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L148) (`StdioTransport_ShouldThrowOnInvalidExecutable`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L210`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L210) (`StdioTransport_ShouldTimeoutOnSlowRequests`)
  - [Backend xUnit] [`ModelContextGateway.Tests/StdioTransportTests.cs#L289`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L289) (`StdioTransport_ShouldHandleUnexpectedExit`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L94`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L94) (`GetSecretAsync_ThrowsInvalidOperationException_WhenTokenEndpointMissing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L102`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L102) (`GetSecretAsync_ThrowsSecurityException_WhenHttpResponseIsNotSuccess`)
  - [Playwright E2E] [`frontend/e2e/multi-user-matrix.spec.ts#L55`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/multi-user-matrix.spec.ts#L55) (`Guest / Denied Context: restricted user session renders safely`)

### `[GUARD-04]` Malformed completion payloads or unmapped backends must fail closed safely
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (20):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L508`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L508) (`Pairwise_CompleteAsync_MalformedOrMissingBackends_ThrowsOrFailsClosed`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L531`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L531) (`Pairwise_DatabaseDisconnection_FailsClosedSafely`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L163`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L163) (`SchemaValidation_FailsClosed_WhenRequiredColumnOrTableMissing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L189`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L189) (`SchemaValidation_FailsClosed_WhenUserQuotasOrKeyTypeMissing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L587`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L587) (`SchemaValidation_FailsClosed_WhenOAuthClientsTableMissing`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L449`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L449) (`Controllers_HandleDbFailures_Returning500`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L64`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L64) (`GetAllProviders_Returns500_OnDbException`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L140`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L140) (`SaveSecretProvider_Returns500_WhenRepositoryThrows`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L206`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L206) (`SaveAuthProvider_Returns500_WhenRepositoryThrows`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L227`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L227) (`GetSecretProviders_Returns500_OnDbException`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L242`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L242) (`GetAuthProviders_Returns500_OnDbException`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L216`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L216) (`AuditLogger_ThrowsException_OnDatabaseError`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L240`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L240) (`CallTool_FailsClosed_WhenAuditLogFails`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L279`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L279) (`CallTool_FailsClosed_WhenAuditLoggerUnresolved`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditLoggerTests.cs#L91`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditLoggerTests.cs#L91) (`LogInvocationAsync_ThrowsInvalidOperationException_OnConnectionFailure`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuditLoggerTests.cs#L104`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditLoggerTests.cs#L104) (`LogAdminActionAsync_ThrowsInvalidOperationException_OnConnectionFailure`)
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L173`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L173) (`RBAC_DefaultsToDenied_WhenDbExceptionThrown`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L89`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L89) (`AuditLogger_AuditFailClosed_RefusesInvocation_OnAuditWriteError`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L217`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L217) (`DeleteMapping_Returns500_OnDbException`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L230`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L230) (`GetPolicies_Returns500_OnDbException`)

### `[GUARD-05]` Socket-level SSRF protection blocks private and loopback IP connections unless explicitly allowlisted.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (16):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L712`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L712) (`Connect_BlocksPrivateOrLoopbackIPs_AtSocketLevel`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L744`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L744) (`SecurityValidationHelper_IsBlockedIp_ValidatesAllBlockedAndAllowedRanges`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L84`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L84) (`PrivateOrLoopback_Blocked_When_AllowPrivateIps_False`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L256`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L256) (`FailClosedValidation_RejectsInvalidJson_AndInsecureUrls`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L103`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L103) (`SaveSecretProvider_ReturnsBadRequest_WhenHttpUrlPassedInConfig`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L330`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L330) (`SaveAuthProvidersBatch_ReturnsBadRequest_WhenAllProvidersDisabled`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L397`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L397) (`SaveSecretProvider_HttpUrl_RejectedForExternal`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L28`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L28) (`IsValidServerUrl_Rejects_Invalid_Http_Urls`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L53) (`Validation_Rejects_TypeOnly_Update_Leaving_Incompatible_Url`)
  - [Backend xUnit] [`ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L232`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L232) (`ProbeServerAsync_Sets_Failed_For_Invalid_Stdio_Server_Command`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L59`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L59) (`DockerDiscovery_SkipsContainer_ResolvingToPrivateIp`)
  - [Backend xUnit] [`ModelContextGateway.Tests/EmbeddingServiceTests.cs#L83`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EmbeddingServiceTests.cs#L83) (`ApiEmbeddingService_GetEmbeddingAsync_Throws_On_Http_Error`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L58`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L58) (`McpClient_NamedHttpClient_Applies_SsrfConnectCallback_AndBlocksPrivateIps`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L1054`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L1054) (`CustomFilesSanitization_PreventsDirectoryTraversal`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L7) (`IsBlockedIp_ValidatesSpecialIpRanges`)
  - [Backend xUnit] [`ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L31`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L31) (`IsInSubnet_HandlesSpecialCases`)

### `[GUARD-06]` Auth middleware enforces case-insensitive route matching preventing path bypass.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (13):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ChallengerTests.cs#L216`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L216) (`AuthMiddleware_CaseInsensitivity_Bypass_Check`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L52`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L52) (`HeaderAuth_StripsHeaders_ForUntrustedProxy`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L330`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L330) (`TrustedProxyHelper_DeniesLoopback_WhenNotExplicitlyAllowlisted`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L349`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L349) (`TrustedProxyHelper_DeniesXForwardedFor_WhenChainHasUntrustedHop`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L390`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L390) (`TrustedProxyHelper_Unconfigured_LoopbackTrusted_LANNotTrusted`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L412`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L412) (`TrustedProxyHelper_ConfiguredProxyTrusted`)
  - [Backend xUnit] [`ModelContextGateway.Tests/IdentityProviderTests.cs#L458`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L458) (`TrustedProxyHelper_ForgedHeaderFromLanHost_DegradesToGuest`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CorsTests.cs#L20`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CorsTests.cs#L20) (`Cors_DefaultFallback_Allows_LocalhostOrigins`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CorsTests.cs#L50`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CorsTests.cs#L50) (`Cors_DefaultFallback_Denies_In_Production`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CorsTests.cs#L92`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CorsTests.cs#L92) (`Cors_WithConfiguredOrigins_RestrictsToConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CorsTests.cs#L121`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CorsTests.cs#L121) (`Cors_WithAllowedOriginsKeyFallback_RestrictsToConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L243`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L243) (`SavePolicy_ReturnsBadRequest_WhenWildcardDenyPolicy`)
  - [Backend xUnit] [`ModelContextGateway.Tests/OpenIddictProductionTests.cs#L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OpenIddictProductionTests.cs#L12) (`Production_WithNoCert_Throws_InvalidOperationException`)

### `[GUARD-ADMIN-CUSTOM-FILES-VALIDATION]` manage_custom_files rejects invalid prompt JSON syntax and unsupported file categories.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L779`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L779) (`ManageCustomFiles_ValidationGuardrails`)

### `[GUARD-ADMIN-ENDPOINT-UNAUTHORIZED]` Unauthenticated / non-admin client request to /admin receives 403 Forbidden.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminEndpointsTests.cs#L193`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L193) (`AdminEndpoint_UnauthorizedCaller_Returns403`)

### `[GUARD-ADMIN-POLICIES-WILDCARD-DENY]` manage_policies rejects wildcard deny policies to prevent global lockout.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L513`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L513) (`ManagePolicies_WildcardDenyGuardrail`)

### `[GUARD-ADMIN-PROVIDERS-LDAP-PLAINTEXT]` manage_providers rejects unencrypted LDAP connections on port 389.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L650`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L650) (`ManageProviders_LdapPlaintextGuardrail`)

### `[GUARD-ADMIN-SERVERS-VALIDATION]` Verifies that the manage_servers tool accurately enforces validation by rejecting malformed transport types, missing required parameters, and requests for non-existent servers.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminToolsParityTests.cs#L324`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L324) (`ManageServers_ValidationGuardrails`)

### `[GUARD-ADMIN-UNKNOWN-TOOL]` AdminMcpServer returns an error response for unknown tool or action invocations.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L596`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L596) (`CallToolAsync_UnknownToolOrAction_ReturnsErrorResponse`)

### `[GUARD-APPKEY-EMPTY-CATEGORY]` AppKey creation with empty or whitespace category must fail closed with BadRequest
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L239`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L239) (`AppKeysController_CreateAppKey_EmptyCategory_FailsWithBadRequest`)

### `[GUARD-APPKEY-MALFORMED-SCOPES]` Corrupted AppKey scopes JSON must fail closed and reject execution
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L490`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L490) (`Pairwise_CorruptedAppKeyScopesJson_FailsClosed_ReturnsFalse`)

### `[GUARD-APPKEY-MISSING-NAME]` Rejects AppKey creation with BadRequest when name is missing.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L312`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L312) (`CreateAppKey_ReturnsBadRequest_WhenNameMissing`)

### `[GUARD-APPKEY-QUOTA-INVALID-PARAM]` Returns BadRequest on invalid quota override input parameters.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L433`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L433) (`QuotaEndpoints_Validation_ReturnsBadRequest_OnInvalidInputs`)

### `[GUARD-APPKEY-REVOKE-FORBID]` Returns Forbid when non-owner/non-admin attempts to revoke an AppKey.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L378`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L378) (`RevokeAppKey_ReturnsForbid_WhenUserNotOwnerOrAdmin`)

### `[GUARD-APPKEY-REVOKE-NOTFOUND]` Returns NotFound when revoking non-existent AppKey ID.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L369`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L369) (`RevokeAppKey_ReturnsNotFound_WhenIdDoesNotExist`)

### `[GUARD-APPKEY-UNKNOWN-CATEGORY]` Non-admin callers cannot create AppKeys with unconfigured categories
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L220`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L220) (`AppKeysController_CreateAppKey_UnknownCategory_NonAdmin_FailsWithBadRequest`)

### `[GUARD-APPKEY-USER-QUOTA]` Enforces user AppKey limit and returns BadRequest when quota is exceeded.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeysControllerTests.cs#L324`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L324) (`CreateAppKey_EnforcesUserLimit_ForNonAdmin`)

### `[GUARD-AUTH-MIDDLEWARE-UNAUTHORIZED]` Auth middleware blocks unauthorized requests with HTTP 401 Unauthorized.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/McpIntegrationTests.cs#L591`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L591) (`AuthMiddleware_Blocks_Unauthorized_Request`)

### `[GUARD-AUTH-NULL-TARGET]` Null or empty capability targets must immediately fail closed and return unauthorized
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L468`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L468) (`Pairwise_NullOrEmptyTarget_FailsClosed_ReturnsFalse`)

### `[GUARD-CLIENT-CLEANUP-REPO-ERR]` CleanupClients returns 500 when repository throws.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L352`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L352) (`CleanupClients_Returns500_WhenOAuthClientRepositoryThrows`)

### `[GUARD-CLIENT-CREATE-REPO-ERR]` CreateClient returns 500 when repository throws
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L300`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L300) (`CreateClient_Returns500_WhenOAuthClientRepositoryThrows`)

### `[GUARD-CLIENT-DELETE-REPO-ERR]` DeleteClient returns 500 when repository throws
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L317`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L317) (`DeleteClient_Returns500_WhenOAuthClientRepositoryThrows`)

### `[GUARD-CLIENT-EMPTY-CATEGORY]` Client creation with empty category scope must fail closed with BadRequest
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L304`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L304) (`ClientsController_CreateClient_EmptyCategory_ReturnsBadRequest`)

### `[GUARD-CLIENT-EMPTY-SCOPE]` CreateClient fails closed when category scope is empty
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L283`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L283) (`CreateClient_ReturnsBadRequest_WhenCategoryScopeEmpty`)

### `[GUARD-CLIENT-MISSING-NAME]` CreateClient fails closed when DisplayName is missing
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L270`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L270) (`CreateClient_ReturnsBadRequest_WhenDisplayNameMissing`)

### `[GUARD-HTTP-NO-SECRET-RETRIEVER]` HTTP stateless transport fails closed with InvalidOperationException when no secret retriever is configured
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/HttpTransportTests.cs#L54`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L54) (`ResolveTokenAsync_ThrowsInvalidOperationException_WhenNoRetrieverRegistered`)

### `[GUARD-HTTP-SECRET-RETRIEVAL-FAIL-CLOSED]` HTTP stateless transport fails closed with SecurityException when secret resolution fails
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/HttpTransportTests.cs#L31`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L31) (`ResolveTokenAsync_ThrowsSecurityException_WhenSecretProviderFails`)

### `[GUARD-MAPPING-GET-DB-ERROR]` PermissionsController returns 500 on database error during mapping retrieval.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L147`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L147) (`GetMappings_Returns500_OnDbException`)

### `[GUARD-MAPPING-MISSING-EXT-ID]` PermissionsController rejects mapping save missing external ID with BadRequest.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L160`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L160) (`SaveMapping_ReturnsBadRequest_WhenExternalIdMissing`)

### `[GUARD-MAPPING-MISSING-GROUP]` PermissionsController rejects mapping save missing internal group with BadRequest.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L171`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L171) (`SaveMapping_ReturnsBadRequest_WhenInternalGroupMissing`)

### `[GUARD-MAPPING-SAVE-DB-ERROR]` PermissionsController returns 500 when saving mapping encounters DB error.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L193`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L193) (`SaveMapping_Returns500_OnDbException`)

### `[GUARD-MAPPING-UNMAPPED-DENY]` Fails closed and denies access when no valid group mapping exists for a restricted target.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/GroupMappingsAndSpecAuthTests.cs#L125`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GroupMappingsAndSpecAuthTests.cs#L125) (`GroupMapping_RejectsUser_WhenNoMappingExistsForRestrictedTarget`)

### `[GUARD-POLICY-DELETE-DB-ERROR]` PermissionsController fails closed with 500 when database delete fails.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L124`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L124) (`DeletePolicy_Returns500_OnDbException`)

### `[GUARD-POLICY-MISSING-GROUP]` PermissionsController rejects policy saves missing requiredGroup with BadRequest.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L69`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L69) (`SavePolicy_ReturnsBadRequest_WhenRequiredGroupMissing`)

### `[GUARD-POLICY-MISSING-TARGET]` PermissionsController rejects policy saves missing targetId with BadRequest.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PermissionsControllerTests.cs#L58`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L58) (`SavePolicy_ReturnsBadRequest_WhenTargetIdMissing`)

### `[GUARD-PROVIDER-MISSING-AUTH-NAME]` ProvidersController rejects saving auth provider without providerName with BadRequest.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L172`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L172) (`SaveAuthProvider_ReturnsBadRequest_WhenProviderNameMissing`)

### `[GUARD-PROVIDER-MISSING-SECRET-NAME]` ProvidersController rejects saving secret provider without providerName with BadRequest.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ProvidersControllerTests.cs#L90`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L90) (`SaveSecretProvider_ReturnsBadRequest_WhenProviderNameMissing`)

### `[GUARD-RBAC-COMPLETION-PROMPT]` completion/complete throws UnauthorizedAccessException when caller lacks prompt permissions.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L613`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L613) (`CompleteAsync_ForPrompt_ThrowsUnauthorized_WhenCallerDenied`)

### `[GUARD-RBAC-COMPLETION-TEMPLATE]` completion/complete throws UnauthorizedAccessException when caller lacks resource template permissions.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L645`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L645) (`CompleteAsync_ForResourceTemplate_ThrowsUnauthorized_WhenCallerDenied`)

### `[GUARD-RBAC-COMPLETION-UNRESOLVED]` completion/complete fails closed on unknown or unresolved completion references.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L677`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L677) (`CompleteAsync_FailsClosed_OnUnknownOrUnresolvedTargets`)

### `[GUARD-RBAC-DEFAULT-DENY]` RBAC defaults to deny when no matching access policies are configured.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L84`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L84) (`RBAC_DefaultsToDenied_WhenNoPoliciesConfigured`)
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L177`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L177) (`NonAdmin_DefaultsToDeny_WhenNoMatchingPoliciesConfigured`)

### `[GUARD-RBAC-EXPLICIT-DENY]` RBAC enforces explicit policy denials to reject unauthorized callers.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L118`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L118) (`RBAC_RejectsUser_OnExplicitDeny`)
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L235`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L235) (`ExplicitDeny_OverridesGroupAllow`)

### `[GUARD-RBAC-MISSING-GROUP]` RBAC denies access when user does not possess the required security group.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L106`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L106) (`RBAC_RejectsUser_WhenPolicyRequiresDifferentGroup`)

### `[GUARD-RBAC-NULL-TARGET]` IsUserAuthorizedAsync fails closed on null, empty, or whitespace target identifiers.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L198`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L198) (`IsUserAuthorizedAsync_FailsClosed_OnNullOrWhitespaceTarget`)

### `[GUARD-RBAC-PROMPT-UNAUTHORIZED]` GetPromptAsync throws UnauthorizedAccessException when user lacks permissions for target prompt.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L145`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L145) (`GetPromptAsync_ThrowsUnauthorized_WhenUnauthorized`)

### `[GUARD-RBAC-RESOURCE-UNAUTHORIZED]` ReadResourceAsync throws UnauthorizedAccessException when user lacks permissions for target resource.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L159`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L159) (`ReadResourceAsync_ThrowsUnauthorized_WhenUnauthorized`)

### `[GUARD-RBAC-TOOL-UNAUTHORIZED]` CallToolAsync returns a formatted security error when user is unauthorized.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/FineGrainedRbacTests.cs#L130`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L130) (`CallToolAsync_ReturnsError_WhenUnauthorized`)

### `[GUARD-ROUTING-UNKNOWN-TOOL]` ToolRoutingManager throws KeyNotFoundException when calling a tool not registered in the routing table.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L187`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L187) (`CallToolAsync_ThrowsKeyNotFound_WhenToolNotInRoutingTable`)

### `[GUARD-ROUTING-UNREGISTERED-RESOURCE]` ResourceRoutingManager throws KeyNotFoundException when reading an unregistered resource URI.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L67`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L67) (`ReadResourceAsync_ThrowsKeyNotFound_WhenResourceNotRegistered`)

### `[GUARD-STATE-DISCONNECT-CANCELLATION]` JsonRpcStateManager rejects registration and cancels pending completions upon disconnect.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L578`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L578) (`JsonRpcStateManager_Disconnect_PreventsRegistrationAndCancelsPending`)

### `[GUARD-TOOL-CANCELLATION]` ToolRoutingManager propagates task cancellation gracefully with a standardized JSON-RPC error response.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L155`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L155) (`CallToolAsync_ReturnsCancellationError_WhenCancelled`)

### `[GUARD-TOOL-MANDATORY-PARAMS]` ToolRoutingManager returns an error when execute_tool is invoked without the mandatory tool name parameter.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L125`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L125) (`CallToolAsync_ExecuteTool_ReturnsError_WhenNameMissing`)

### `[GUARD-UNSUPPORTED-DB-PROVIDER]` DbConnectionFactory fails closed and throws InvalidOperationException when configured with unsupported database provider.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L29`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L29) (`DbConnectionFactory_Throws_OnUnsupportedProvider`)

### `[GUARD-VALIDATION-STDIO-SHELL-OPERATORS]` ServerValidationHelper validates stdio commands against unsafe shell operators, piping, and command injection.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L7) (`IsValidStdioCommand_ValidatesExecutableAndDisallowsUnsafeCommands`)

### `[MCP-ADMIN-TOOL-TEST-CALL-ERROR]` AdminMcpServer test_tool_call propagates downstream backend errors with visibility.
* **Category:** `GUARD` (Universal Safety & Fail-Closed Guardrails)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L637`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L637) (`CallToolAsync_TestToolCall_MissingServer_ReturnsError`)

### `[MCP-22]` AdminMcpServer ProcessRequestAsync handles server/discover request returning supported versions and subscriptions capability.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AdminMcpServerTests.cs#L686`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L686) (`ProcessRequestAsync_Handles_Server_Discover`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L371`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L371) (`DownstreamBackend_ProtocolVersionMismatch_NegotiatesOlderVersionSuccessfully`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L124`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L124) (`Middleware_Extracts_Stateless_Capabilities_And_ClientInfo_In_Meta`)
  - [Backend xUnit] [`ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L162`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L162) (`Middleware_Rejects_Unsupported_Protocol_Version_With_32021_Error`)

### `[MCP-28]` ToolRoutingManager rejects ambiguous bare tool calls when duplicate tool names exist across distinct servers, listing candidates with namespaces.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L489`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L489) (`NormalizeTargetToolName_Returns_Ambiguity_Error_Listing_Aliases_For_Duplicates`)

### `[MCP-29]` ServerValidationHelper rejects invalid characters in Alias.
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (5):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L72`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L72) (`ValidateServer_Rejects_Invalid_Alias_Characters`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L89`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L89) (`ValidateServer_Rejects_Alias_Colliding_With_Existing_ServerId`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L104`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L104) (`ValidateServer_Rejects_Alias_Colliding_With_Existing_Server_Alias`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L119`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L119) (`ValidateServer_Accepts_Valid_Alias_And_Self_Retention`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L141`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L141) (`ValidateServer_Rejects_CaseInsensitive_Collisions`)

### `[MCP-31]` DockerAutoDiscoveryService parses mcp.alias from Docker container labels
* **Category:** `MCP` (Model Context Protocol Engine & Tool Routing)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (4):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L188`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L188) (`ParseDiscoveredServers_Parses_McpAlias_Label`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L213`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L213) (`ParseDiscoveredServers_Parses_McpNamespace_Fallback_Label`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L237`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L237) (`ParseDiscoveredServers_Ignores_Invalid_Alias_Characters`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L261`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L261) (`UpsertDiscoveredServers_PreservesExistingDbAlias_AndInsertsDiscoveredAlias`)

### `[AUTH-106]` Exchange throws InvalidOperationException when request is null.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L19`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L19) (`Exchange_ThrowsInvalidOperationException_WhenRequestNull`)

### `[AUTH-108]` Authorize throws InvalidOperationException when OIDC request is null.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L77`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L77) (`Authorize_ThrowsInvalidOperationException_WhenRequestNull`)

### `[AUTH-111]` Pipeline exposes RFC 9728 OAuth Protected Resource discovery endpoints with dynamic resource identifiers.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (8):**
  - [Backend xUnit] [`ModelContextGateway.Tests/PipelineIntegrationTests.cs#L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L62) (`Pipeline_WellKnown_Endpoints_ReturnSuccess`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L159`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L159) (`Exchange_ClientCredentials_ValidSecret_ReturnsSignInResult`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L206`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L206) (`Exchange_ClientCredentials_InvalidSecret_ReturnsForbid`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L251`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L251) (`Exchange_ClientCredentials_ExpiredClient_ReturnsForbid`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L84`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L84) (`CreateClient_ReturnsOk_WithGeneratedCredentials`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L153`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L153) (`DatabaseAssertion_PlaintextNotPersisted`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L179`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L179) (`CreateClient_AdminCreator_DoesNotInheritAdminSid`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L215`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L215) (`CreateClient_WithExpiresInDays_SetsExpiration`)

### `[AUTH-112]` Authorize resolves client application from IOAuthClientRepository and redirects to consent.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (3):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L297`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L297) (`Authorize_ResolvesClientAndRedirectsToConsent`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L120`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L120) (`DeleteClient_ReturnsNoContent_WhenAppExists`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ClientsControllerTests.cs#L141`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L141) (`DeleteClient_ReturnsNotFound_WhenAppDoesNotExist`)

### `[AUTH-114]` RegisterClient rejects invalid or non-absolute redirect URIs with standard RFC 7591 invalid_redirect_uri error.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L400`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L400) (`RegisterClient_InvalidRedirectUri_ReturnsBadRequest`)

### `[AUTH-116]` Exchange rejects client_credentials grant attempts by public clients with UnauthorizedClient error.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L475`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L475) (`Exchange_PublicClient_ClientCredentials_ReturnsForbid`)

### `[AUTH-117]` RegisterClient returns 403 Forbidden with access_denied when open client registration is disabled and caller is unauthorized.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/AuthorizationControllerTests.cs#L516`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L516) (`RegisterClient_WhenClosedRegistration_UnauthorizedUser_ReturnsForbidden`)

### `[SEC-01]` SQLite database is encrypted at rest using SQLCipher with DB_ENCRYPTION_KEY.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (17):**
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseEncryptionTests.cs#L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseEncryptionTests.cs#L8) (`SqliteDatabase_IsEncrypted_WithSQLCipher`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L70`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L70) (`SaveSecretProvider_EncryptsConfigJson_AtRestInDatabase`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L99`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L99) (`SaveAuthProvider_EncryptsConfigJson_AtRestInDatabase`)
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L218`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L218) (`ProvidersController_MaskPreserving_PreservesExistingDecryptedSecret_WhenMaskSubmitted`)
  - [Backend xUnit] [`ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L72`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L72) (`SymmetricEncryptionHelper_EncryptsAndDecryptsCorrectly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CorsTests.cs#L146`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CorsTests.cs#L146) (`Cors_RejectsWildcardAndInvalidOrigins_AndLogsWarning`)
  - [Backend xUnit] [`ModelContextGateway.Tests/CorsTests.cs#L181`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CorsTests.cs#L181) (`Cors_AllInvalidOrigins_FallsBackToDefaultAndLogsWarning`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L127`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L127) (`GetSecretAsync_ThrowsHttpRequestException_WhenHttpClientFails`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L165`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TokenExchangeSecretRetrieverTests.cs#L165) (`GetSecretAsync_ThrowsJsonException_WhenResponseIsInvalidJson`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L14`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L14) (`EnsureVaultClientAsync_CreatesClient_WithAppRoleCredentials`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L35`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L35) (`EnsureVaultClientAsync_LoadsFromSecretRepo_WhenConfigJsonHasAppRole`)
  - [Backend xUnit] [`ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L87`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/VaultAppRoleAndRenewalTests.cs#L87) (`ReloadConfigAsync_ClearsClient_ForcesRecreation`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L52`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L52) (`DbEncryptionKey_Warning_Detection_Works_Correctly`)
  - [Backend xUnit] [`ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L70`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L70) (`Startup_MigratesLegacyKeysToHashedKeys`)
  - [Frontend Vitest] [`frontend/src/test/components/SecretProvidersTab.test.tsx#L19`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SecretProvidersTab.test.tsx#L19) (`renders provider inputs and submits updated configuration`)
  - [Frontend Vitest] [`frontend/src/test/components/SecretProvidersTab.test.tsx#L90`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SecretProvidersTab.test.tsx#L90) (`handles Test Vault connection button with success and failure responses`)
  - [Playwright E2E] [`frontend/e2e/full-ui-flow-sse-vault.spec.ts#L8`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/full-ui-flow-sse-vault.spec.ts#L8) (`should register SSE server with Vault provider (Mount/Path/Field), verify badge, and run semantic search`)

### `[SEC-PROVIDER-GUARD-CORRUPT-ENCRYPTED-FIELD]` Router must not overwrite corrupt encrypted database fields if an update occurs without user reset.
* **Category:** `SEC` (Secrets Providers & Encryption)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L388`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L388) (`SaveSecretProvider_WhenDecryptionFailed_DoesNotOverwriteCorruptPayload`)

### `[TRANS-HTTP-DISPOSED-GUARD]` HttpTransport SendRequestAsync returns -32001 Not Connected when transport has been disposed.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (2):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportResilienceTests.cs#L59`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L59) (`HttpTransport_SendRequestAsync_ReturnsDisposedError_WhenDisposed`)
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportResilienceTests.cs#L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L80) (`HttpTransport_CallMethodAsync_ReturnsDisposedError_WhenDisposed`)

### `[TRANS-SSE-CALLMETHOD-DISCONNECT-GUARD]` SseTransport CallMethodAsync returns -32001 Not Connected when backend is disconnected.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportResilienceTests.cs#L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L12) (`SseTransport_CallMethodAsync_ReturnsNotConnected_WhenDisconnected`)

### `[TRANS-SSE-SENDREQUEST-DISCONNECT-GUARD]` SseTransport SendRequestAsync returns -32001 Not Connected when backend is disconnected.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportResilienceTests.cs#L36`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L36) (`SseTransport_SendRequestAsync_ReturnsNotConnected_WhenDisconnected`)

### `[TRANS-STDIO-DISPOSED-GUARD]` StdioTransport SendRequestAsync returns -32001 Process Not Running when transport has been disposed.
* **Category:** `TRANS` (Transports (SSE, HTTP, STDIO, Proxy))
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (1):**
  - [Backend xUnit] [`ModelContextGateway.Tests/TransportResilienceTests.cs#L101`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L101) (`StdioTransport_SendRequestAsync_ReturnsProcessNotRunning_WhenDisposed`)

### `[UI-31]` Fetches registered OAuth clients and updates store state.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (7):**
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L37`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L37) (`fetches registered clients and updates state`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L213`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L213) (`prompts confirmation and calls cleanupClientsApi when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L239`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L239) (`cancels DCR cleanup when user cancels confirmation modal`)
  - [Frontend Vitest] [`frontend/src/test/components/RegisteredClientsCard.test.tsx#L42`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/RegisteredClientsCard.test.tsx#L42) (`renders header, register button, and calls fetchClients on mount`)
  - [Frontend Vitest] [`frontend/src/test/components/RegisteredClientsCard.test.tsx#L73`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/RegisteredClientsCard.test.tsx#L73) (`renders empty state when no registered clients exist`)
  - [Frontend Vitest] [`frontend/src/test/components/RegisteredClientsCard.test.tsx#L90`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/RegisteredClientsCard.test.tsx#L90) (`renders rich client columns and handles client ID copy`)
  - [Frontend Vitest] [`frontend/src/test/components/RegisteredClientsCard.test.tsx#L136`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/RegisteredClientsCard.test.tsx#L136) (`triggers deleteClient when Delete button is clicked`)

### `[UI-CONFIRM-MODAL]` Centralized promise-based confirmation store resolves true on confirmation and false on cancellation.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (16):**
  - [Frontend Vitest] [`frontend/src/test/stores/useConfirmStore.test.ts#L4`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useConfirmStore.test.ts#L4) (`initializes in closed state`)
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L100`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L100) (`deletes a policy when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L129`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L129) (`does not delete policy when confirm is cancelled`)
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L202`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L202) (`deletes a group mapping when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L231`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L231) (`does not delete group mapping when confirm is cancelled`)
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L258`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L258) (`deletes a custom file when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/usePolicyStore.test.ts#L287`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L287) (`does not delete custom file when confirm is cancelled`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L141`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L141) (`prompts confirmation and deletes client when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L170`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L170) (`cancels deletion when user denies confirmation`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L487`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L487) (`confirms and revokes AppKey and refreshes list`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L516`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L516) (`cancels revocation when confirm is rejected`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L632`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L632) (`prompts confirmation modal and resets user quota when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/useClientStore.test.ts#L662`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L662) (`cancels quota reset when user denies confirmation`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L269`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L269) (`prompts window.confirm and deletes server when confirmed`)
  - [Frontend Vitest] [`frontend/src/test/stores/useServerStore.test.ts#L298`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L298) (`does not send delete request when confirm is cancelled`)
  - [Frontend Vitest] [`frontend/src/test/components/ConfirmModal.test.tsx#L6`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ConfirmModal.test.tsx#L6) (`renders nothing when closed`)

### `[UI-TOAST-TRANSITION]` Displays error toast notification when saving invalid JSON credentials for user-provided server.
* **Category:** `UI` (Dashboard, Test Bench & Settings UI)
* **Type:** Negative / Safety Guardrail (Fail-Closed)
* **Verification Proofs (8):**
  - [Frontend Vitest] [`frontend/src/test/pages/MyMcpServers.test.tsx#L23`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/pages/MyMcpServers.test.tsx#L23) (`shows error toast when saving invalid JSON credentials`)
  - [Frontend Vitest] [`frontend/src/test/pages/MyMcpServers.test.tsx#L64`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/pages/MyMcpServers.test.tsx#L64) (`saves valid credentials successfully and closes modal`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L112`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L112) (`shows error toast when switching from invalid JSON to Visual Prompt Builder`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L133`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L133) (`shows error toast when saving without a file name`)
  - [Frontend Vitest] [`frontend/src/test/components/CustomFileModal.test.tsx#L153`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/CustomFileModal.test.tsx#L153) (`shows error toast when saving prompt with invalid JSON content`)
  - [Frontend Vitest] [`frontend/src/test/components/IdentityAuthTab.test.tsx#L99`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/IdentityAuthTab.test.tsx#L99) (`saves updated Active Directory configuration JSON`)
  - [Frontend Vitest] [`frontend/src/test/components/IdentityAuthTab.test.tsx#L138`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/IdentityAuthTab.test.tsx#L138) (`displays error toast when saving auth providers fails`)
  - [Frontend Vitest] [`frontend/src/test/components/SecretProvidersTab.test.tsx#L63`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SecretProvidersTab.test.tsx#L63) (`displays error toast when saving secret providers fails`)

---

## 4. Complete Verification Traceability Matrix

| Requirement ID | Type | Category | Description | Primary Proof | Suite |
| :--- | :---: | :--- | :--- | :--- | :--- |
| `API-GET-SERVERS-FLEET-REPOSITORY` | Positive | `API` | Retrieves configured MCP server fleet from the database repository. | [`MinimalApiEndpointsTests.cs:L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MinimalApiEndpointsTests.cs#L42) | Backend xUnit |
| `API-SERVER-CRUD-LIFECYCLE` | Positive | `API` | Supports full CRUD lifecycle (create, read, update, delete) for downstream MCP server definitions. | [`MinimalApiEndpointsTests.cs:L55`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MinimalApiEndpointsTests.cs#L55) | Backend xUnit |
| `AUTH-001` | Positive | `AUTH` | Verify DatabaseUserSecretStore encrypts and decrypts secret correctly. | [`UserSecretStoreTests.cs:L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserSecretStoreTests.cs#L8) | Backend xUnit |
| `AUTH-002` | Positive | `AUTH` | Verify UserCredentialsController returns configured server IDs. | [`UserCredentialsControllerTests.cs:L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserCredentialsControllerTests.cs#L11) | Backend xUnit |
| `AUTH-02` | Positive | `AUTH` | AppKey scopes restrict access precisely across all MCP capabilities and backend targets | [`PairwiseIntegrationMatrixTests.cs:L242`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L242) | Backend xUnit |
| `AUTH-03` | Positive | `AUTH` | Auth middleware allows bypass routes and extracts SSO headers in a case-insensitive manner. | [`ChallengerTests.cs:L603`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L603) | Backend xUnit |
| `AUTH-04` | Positive | `AUTH` | ActiveDirectoryIdentityProvider extracts Windows caller SIDs and security groups via IWindowsIdentityAccessor and augments with LDAP | [`ActiveDirectoryWindowsIdentityTests.cs:L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ActiveDirectoryWindowsIdentityTests.cs#L12) | Backend xUnit |
| `AUTH-05` | Positive | `AUTH` | McpServer supports AllowPassThroughAuth flag | [`McpServerTests.cs:L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpServerTests.cs#L5) | Backend xUnit |
| `AUTH-06` | Positive | `AUTH` | Transports use passThroughToken when AllowPassThroughAuth is true | [`TransportsAuthShapeTests.cs:L208`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L208) | Backend xUnit |
| `AUTH-101` | Positive | `AUTH` | HTTP transport injects X-Forwarded-User header based on connected user identity. | [`IdentityHeaderTests.cs:L9`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityHeaderTests.cs#L9) | Backend xUnit |
| `AUTH-110` | Positive | `AUTH` | CreateAppKey allows creating unlimited AppKeys when UserMaxKeys is set to 0. | [`AppKeysControllerTests.cs:L343`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L343) | Backend xUnit |
| `AUTH-118` | Positive | `AUTH` | FindDcrClientAsync resolves existing DCR client matching client name and type. | [`OAuthClientRepositoryTests.cs:L214`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L214) | Backend xUnit |
| `AUTH-119` | Positive | `AUTH` | CleanupDcrClientsAsync prunes duplicate and expired dynamic client registrations across all database providers. | [`OAuthClientRepositoryTests.cs:L237`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OAuthClientRepositoryTests.cs#L237) | Backend xUnit |
| `AUTH-14` | Positive | `AUTH` | Tool execution catches 401 Unauthorized from downstream target servers and returns interactive auth remediation. | [`ToolRoutingManagerTests.cs:L211`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L211) | Backend xUnit |
| `AUTH-15` | Positive | `AUTH` | OpenIddict initializes ephemeral development signing certificates in Development environment. | [`OpenIddictProductionTests.cs:L30`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/OpenIddictProductionTests.cs#L30) | Backend xUnit |
| `AUTH-35` | Positive | `AUTH` | Single-user homelab startup initializes SQLite, auto-generates Admin and Client AppKeys without PFX certificate requirements | [`SingleUserHomelabTests.cs:L30`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L30) | Backend xUnit |
| `AUTH-36` | Positive | `AUTH` | Pre-configured MCG_CLIENT_APP_KEYS seeds functional individualized client keys with custom scopes | [`SingleUserHomelabTests.cs:L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L98) | Backend xUnit |
| `AUTH-37` | Positive | `AUTH` | AppKeys with server and category scopes enforce precise tool execution boundaries | [`SingleUserHomelabTests.cs:L168`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L168) | Backend xUnit |
| `AUTH-38` | Positive | `AUTH` | LAN CIDR network configuration allows standalone web dashboard access from local subnet | [`SingleUserHomelabTests.cs:L183`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L183) | Backend xUnit |
| `AUTH-39` | Positive | `AUTH` | Zero-config startup defaults enterprise auth providers and secret providers to disabled | [`SingleUserHomelabTests.cs:L211`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SingleUserHomelabTests.cs#L211) | Backend xUnit |
| `AUTH-ADMIN-FULL-ACCESS` | Positive | `AUTH` | Administrator identities bypass granular capability policies and have full access to all MCP methods. | [`UnifiedMcpAuthorizationTests.cs:L154`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L154) | Backend xUnit |
| `AUTH-ADMIN-POLICY-ALLOW-GROUPNAME` | Positive | `AUTH` | AdminPolicy allows principal with configured Admin Group Name (e.g., full_admin) | [`AdminPolicyHybridAuthTests.cs:L13`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L13) | Backend xUnit |
| `AUTH-ADMIN-POLICY-ALLOW-GROUPS-ARRAY` | Positive | `AUTH` | AdminPolicy allows principal with configured Admin Groups array | [`AdminPolicyHybridAuthTests.cs:L81`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L81) | Backend xUnit |
| `AUTH-ADMIN-POLICY-ALLOW-SID` | Positive | `AUTH` | AdminPolicy allows principal with configured Admin SID | [`AdminPolicyHybridAuthTests.cs:L47`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L47) | Backend xUnit |
| `AUTH-ADMIN-POLICY-REJECT-REGULAR` | **Guardrail** | `AUTH` | AdminPolicy rejects principal with unconfigured regular role without Admin SID or Admin Group | [`AdminPolicyHybridAuthTests.cs:L116`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminPolicyHybridAuthTests.cs#L116) | Backend xUnit |
| `AUTH-ADMIN-REJECT-NONADMIN` | **Guardrail** | `AUTH` | SecurityValidationHelper rejects non-admin groups and guest identities | [`IdentityProviderTests.cs:L260`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L260) | Backend xUnit |
| `AUTH-ADMIN-VALIDATE-GROUPNAME` | Positive | `AUTH` | SecurityValidationHelper authorizes principals via Admin Group Name | [`IdentityProviderTests.cs:L246`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L246) | Backend xUnit |
| `AUTH-ADMIN-VALIDATE-GROUPS-ARRAY` | Positive | `AUTH` | SecurityValidationHelper authorizes principals via custom configured Admin:Groups array | [`IdentityProviderTests.cs:L277`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L277) | Backend xUnit |
| `AUTH-ADMIN-VALIDATE-MAPPED-GROUPS` | Positive | `AUTH` | SecurityValidationHelper authorizes principals via mappedGroups database resolution | [`IdentityProviderTests.cs:L292`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L292) | Backend xUnit |
| `AUTH-ADMIN-VALIDATE-SID` | Positive | `AUTH` | SecurityValidationHelper authorizes principals via Admin Group SID | [`IdentityProviderTests.cs:L228`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L228) | Backend xUnit |
| `AUTH-APPKEY-ADMIN-FUTURE-CATEGORY` | Positive | `AUTH` | Admin callers can create forward-looking AppKeys for unconfigured categories | [`CategoryScopedAppKeysTests.cs:L258`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L258) | Backend xUnit |
| `AUTH-APPKEY-ADMIN-QUOTA` | Positive | `AUTH` | Administrator can create, update, and delete custom user quota overrides. | [`AppKeysControllerTests.cs:L403`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L403) | Backend xUnit |
| `AUTH-APPKEY-ADMIN-SCOPE-ALLOW` | Positive | `AUTH` | AppKeys with admin scope grant Administrator role and pass AdminPolicy. | [`StandaloneAdminAuthTests.cs:L79`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L79) | Backend xUnit |
| `AUTH-APPKEY-ITEMS-SCOPE-ALLOW` | Positive | `AUTH` | SecurityValidationHelper recognizes admin scopes in HttpContext.Items. | [`StandaloneAdminAuthTests.cs:L255`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L255) | Backend xUnit |
| `AUTH-APPKEY-KEYTYPE-PERSISTENCE-FILTER` | Positive | `AUTH` | IAppKeyRepository persists KeyType and filters keys by personal vs system | [`UserQuotaAndAppKeyRepositoryTests.cs:L146`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L146) | Backend xUnit |
| `AUTH-APPKEY-WILDCARD-SCOPE-ALLOW` | Positive | `AUTH` | AppKeys with wildcard scope '*' grant Administrator role and pass AdminPolicy. | [`StandaloneAdminAuthTests.cs:L140`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L140) | Backend xUnit |
| `AUTH-COMPACT-APPKEY-TAXONOMY` | Positive | `AUTH` | Generates compact ~32-character Base62 AppKeys with semantic prefixes. | [`AppKeyAuthenticationTests.cs:L417`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L417) | Backend xUnit |
| `AUTH-CUSTOM-ADMIN-KEY-SEEDING` | Positive | `AUTH` | Seeds custom MCG_ADMIN_AUTH_KEY when provided in configuration. | [`DatabaseSeederServiceTests.cs:L189`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L189) | Backend xUnit |
| `AUTH-MOCK-SIMULATE-UNAUTHORIZED` | Positive | `AUTH` | MockDownstreamMcpServer simulates 401 Unauthorized status code for authentication testing. | [`MockDownstreamMcpServerTests.cs:L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L62) | Backend xUnit |
| `AUTH-OIDC-PRESERVE-GROUP-NAMES` | Positive | `AUTH` | OidcIdentityProvider preserves group names without synthesizing Windows SIDs | [`IdentityProviderTests.cs:L307`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L307) | Backend xUnit |
| `AUTH-PERM-GET-MAPPINGS` | Positive | `AUTH` | PermissionsController returns group mappings list with 200 OK. | [`PermissionsControllerTests.cs:L137`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L137) | Backend xUnit |
| `AUTH-PERM-POLICY-LIST` | Positive | `AUTH` | PermissionsController returns access policies list with 200 OK. | [`PermissionsControllerTests.cs:L48`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L48) | Backend xUnit |
| `AUTH-PERM-POLICY-REMOVE` | Positive | `AUTH` | PermissionsController removes access policies. | [`PermissionsControllerTests.cs:L114`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L114) | Backend xUnit |
| `AUTH-PERSONAL-APPKEY-CREATE` | **Guardrail** | `AUTH` | Non-admin users can create personal App Keys up to quota | [`AppKeysControllerTests.cs:L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L191) | Backend xUnit |
| `AUTH-PERSONAL-APPKEY-LIST` | Positive | `AUTH` | Non-admin users can view their personal App Keys | [`AppKeysControllerTests.cs:L125`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L125) | Backend xUnit |
| `AUTH-PERSONAL-APPKEY-QUOTA-OVERRIDE` | Positive | `AUTH` | Custom user quotas override default limit | [`AppKeysControllerTests.cs:L223`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L223) | Backend xUnit |
| `AUTH-PIPELINE-ADMIN-DASHBOARD` | Positive | `AUTH` | Dashboard management API suite executes for authorized administrators. | [`PipelineIntegrationTests.cs:L170`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L170) | Backend xUnit |
| `AUTH-PIPELINE-GET-CLIENTS` | Positive | `AUTH` | GET /api/clients returns active client sessions with 200 OK. | [`PipelineIntegrationTests.cs:L348`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L348) | Backend xUnit |
| `AUTH-PIPELINE-GET-POLICIES` | Positive | `AUTH` | GET /api/permissions/policies returns access policies with 200 OK. | [`PipelineIntegrationTests.cs:L357`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L357) | Backend xUnit |
| `AUTH-PIPELINE-PERM-CRUD` | Positive | `AUTH` | Permissions policy and group mapping CRUD endpoints manage RBAC rules. | [`PipelineIntegrationTests.cs:L304`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L304) | Backend xUnit |
| `AUTH-PREFIX-EXTRACTION` | Positive | `AUTH` | ExtractKeyPrefix parses semantic prefixes, Base62 selectors, and legacy tokens accurately. | [`AppKeyAuthenticationTests.cs:L451`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeyAuthenticationTests.cs#L451) | Backend xUnit |
| `AUTH-QUERY-TOKEN-EXTRACTION` | Positive | `AUTH` | Query string token middleware extracts access_token or token query parameter to Authorization header. | [`EndpointAuthorizationTests.cs:L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EndpointAuthorizationTests.cs#L7) | Backend xUnit |
| `AUTH-RBAC-GROUP-ALLOW` | Positive | `AUTH` | RBAC grants access when user claims match the required policy security group. | [`FineGrainedRbacTests.cs:L94`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L94) | Backend xUnit |
| `AUTH-RBAC-PROMPT-FILTER` | Positive | `AUTH` | prompts/list filters exposed prompts according to caller permissions. | [`UnifiedMcpAuthorizationTests.cs:L362`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L362) | Backend xUnit |
| `AUTH-RBAC-RESOURCE-FILTER` | Positive | `AUTH` | resources/list filters exposed resources according to caller permissions. | [`UnifiedMcpAuthorizationTests.cs:L405`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L405) | Backend xUnit |
| `AUTH-RBAC-TEMPLATE-FILTER` | Positive | `AUTH` | resources/templates/list filters exposed resource templates according to caller permissions. | [`UnifiedMcpAuthorizationTests.cs:L448`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L448) | Backend xUnit |
| `AUTH-RBAC-TOOL-FILTER` | Positive | `AUTH` | tools/list filters exposed backend tools according to caller permissions. | [`UnifiedMcpAuthorizationTests.cs:L317`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L317) | Backend xUnit |
| `AUTH-RBAC-TOOLS-FILTER` | Positive | `AUTH` | tools/list filters exposed backend tools according to caller role and RBAC policy permissions. | [`FineGrainedRbacTests.cs:L200`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L200) | Backend xUnit |
| `AUTH-SERVER-LEVEL-POLICY` | Positive | `AUTH` | Server-level access policies authorize all child tools, prompts, and resources under that server. | [`UnifiedMcpAuthorizationTests.cs:L212`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L212) | Backend xUnit |
| `AUTH-SSE-PER-MESSAGE-IDENTITY` | Positive | `AUTH` | SSE streams re-validate caller identity and permissions per message payload. | [`IdentityProviderTests.cs:L121`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/IdentityProviderTests.cs#L121) | Backend xUnit |
| `AUTH-STANDALONE-ADMINPOLICY-LOOPBACK-ALLOW` | Positive | `AUTH` | AdminPolicy succeeds in standalone mode for unauthenticated loopback requests. | [`StandaloneAdminAuthTests.cs:L176`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L176) | Backend xUnit |
| `AUTH-STANDALONE-CUSTOM-CIDR-ALLOW` | Positive | `AUTH` | Standalone mode grants admin access to client IPs matching Admin:StandaloneAllowedNetworks CIDR ranges. | [`StandaloneAdminAuthTests.cs:L35`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L35) | Backend xUnit |
| `AUTH-STANDALONE-LOOPBACK-ALLOW` | Positive | `AUTH` | Standalone mode without external IDP grants admin access to loopback IP addresses. | [`StandaloneAdminAuthTests.cs:L14`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L14) | Backend xUnit |
| `AUTH-STORE-MAPPING-FETCH` | Positive | `AUTH` | fetches group mappings and updates store | [`usePolicyStore.test.ts:L156`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L156) | Frontend Vitest |
| `AUTH-STORE-MAPPING-MODAL-TOGGLE` | Positive | `AUTH` | handles mapping modal open and close | [`usePolicyStore.test.ts:L330`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L330) | Frontend Vitest |
| `AUTH-STORE-MAPPING-SAVE` | Positive | `AUTH` | saves a group mapping and closes mapping modal | [`usePolicyStore.test.ts:L171`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L171) | Frontend Vitest |
| `AUTH-STORE-POLICY-CREATE` | Positive | `AUTH` | creates/saves a policy (ALLOW rule) and closes modal | [`usePolicyStore.test.ts:L53`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L53) | Frontend Vitest |
| `AUTH-STORE-POLICY-FETCH` | Positive | `AUTH` | fetches access policies and updates store | [`usePolicyStore.test.ts:L38`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L38) | Frontend Vitest |
| `AUTH-STORE-POLICY-INIT` | Positive | `AUTH` | initializes with empty policies and mappings | [`usePolicyStore.test.ts:L21`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L21) | Frontend Vitest |
| `AUTH-STORE-POLICY-MODAL-TOGGLE` | Positive | `AUTH` | handles policy modal open and close | [`usePolicyStore.test.ts:L314`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L314) | Frontend Vitest |
| `AUTH-SYSTEM-APPKEY-SEPARATION` | Positive | `AUTH` | System keys are distinct and require admin permissions | [`AppKeysControllerTests.cs:L151`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L151) | Backend xUnit |
| `GUARD-LDAP-FAIL-CLOSED` | **Guardrail** | `AUTH` | LdapActiveDirectoryService fails closed with SecurityException when LDAP connection throws an exception. | [`LdapActiveDirectoryServiceTests.cs:L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L132) | Backend xUnit |
| `UI-100` | Positive | `AUTH` | initializes with empty providers | [`useProviderStore.test.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useProviderStore.test.ts#L1) | Frontend Vitest |
| `UI-101` | Positive | `AUTH` | should initialize with default values | [`useUserStore.test.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L1) | Frontend Vitest |
| `UI-114` | Positive | `AUTH` | renders nothing when isPolicyModalOpen is false | [`PolicyModal.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L1) | Frontend Vitest |
| `UI-120` | Positive | `AUTH` | RBAC and SID mapping administration UI allows configuring role policies and SID associations | [`rbac-enforcement-flow.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/rbac-enforcement-flow.spec.ts#L1) | Playwright E2E |
| `UI-123` | Positive | `AUTH` | should open App Keys & Security view and display client setup controls | [`client-setup-and-appkeys.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/client-setup-and-appkeys.spec.ts#L1) | Playwright E2E |
| `UI-125` | Positive | `AUTH` | Admin role renders full administrative dashboard and server management controls | [`multi-user-matrix.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/multi-user-matrix.spec.ts#L1) | Playwright E2E |
| `UI-127` | Positive | `AUTH` | should navigate to settings permissions tab and open policy configuration modal | [`rbac-and-permissions.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/rbac-and-permissions.spec.ts#L1) | Playwright E2E |
| `UI-129` | Positive | `AUTH` | should create client application and generate AppKey with scope constraints | [`appkey-and-client-lifecycle.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/appkey-and-client-lifecycle.spec.ts#L1) | Playwright E2E |
| `UI-AUTH-TAB-AD-TOGGLE` | Positive | `AUTH` | Renders Active Directory disabled initially, toggles on and exposes fields. | [`IdentityAuthTab.test.tsx:L12`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/IdentityAuthTab.test.tsx#L12) | Frontend Vitest |
| `UI-AUTH-TAB-LDAP-TEST` | Positive | `AUTH` | Fills LDAP parameters and executes test connection. | [`IdentityAuthTab.test.tsx:L46`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/IdentityAuthTab.test.tsx#L46) | Frontend Vitest |
| `UI-POLICY-MODAL-CANCEL-DISMISS` | Positive | `AUTH` | closes modal on cancel click | [`PolicyModal.test.tsx:L87`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L87) | Frontend Vitest |
| `UI-POLICY-MODAL-CREATE-DEFAULTS` | Positive | `AUTH` | renders create policy form with default inputs | [`PolicyModal.test.tsx:L28`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L28) | Frontend Vitest |
| `UI-POLICY-MODAL-EDIT-PREFILL` | Positive | `AUTH` | renders edit policy form pre-filled with policy data | [`PolicyModal.test.tsx:L44`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PolicyModal.test.tsx#L44) | Frontend Vitest |
| `UI-USER-STORE-ERROR-FALLBACK` | Positive | `AUTH` | handles error response gracefully and sets unauthenticated user state | [`useUserStore.test.ts:L50`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L50) | Frontend Vitest |
| `UI-USER-STORE-HEALTH-VERSION` | Positive | `AUTH` | successfully updates version and service from /health endpoint | [`useUserStore.test.ts:L113`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L113) | Frontend Vitest |
| `UI-USER-STORE-LOAD-PROFILE` | Positive | `AUTH` | successfully loads user profile from /api/me | [`useUserStore.test.ts:L23`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L23) | Frontend Vitest |
| `UI-USER-STORE-NETWORK-FAILURE` | Positive | `AUTH` | handles network failure gracefully | [`useUserStore.test.ts:L69`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L69) | Frontend Vitest |
| `UI-USER-STORE-ROLE-EXTRACTION` | Positive | `AUTH` | correctly handles non-admin user role extraction | [`useUserStore.test.ts:L89`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L89) | Frontend Vitest |
| `UI-USER-STORE-VERSION-FALLBACK` | Positive | `AUTH` | keeps existing fallback version on error | [`useUserStore.test.ts:L128`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useUserStore.test.ts#L128) | Frontend Vitest |
| `CORE-101` | Positive | `CORE` | Auto-added requirement tracking | [`SessionManagerTests.cs:L9`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SessionManagerTests.cs#L9) | Backend xUnit |
| `CORE-GATEWAY-METADATA-BUILD-INIT-REQUEST` | Positive | `CORE` | BuildInitializeRequest formats standard JSON-RPC 2.0 initialize request with dynamic protocol version. | [`GatewayMetadataTests.cs:L116`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L116) | Backend xUnit |
| `CORE-GATEWAY-METADATA-CONSTANTS` | Positive | `CORE` | Metadata constants and assembly version return consistent non-empty identifiers. | [`GatewayMetadataTests.cs:L133`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L133) | Backend xUnit |
| `CORE-GATEWAY-METADATA-EXTRACTION-JSONELEMENT` | Positive | `CORE` | ExtractRequestedProtocolVersion parses protocolVersion from JsonElement params object. | [`GatewayMetadataTests.cs:L96`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L96) | Backend xUnit |
| `CORE-GATEWAY-METADATA-EXTRACTION-STRING` | Positive | `CORE` | ExtractRequestedProtocolVersion parses protocolVersion from initialize request payload or isolated params. | [`GatewayMetadataTests.cs:L68`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L68) | Backend xUnit |
| `CORE-GATEWAY-METADATA-SUPPORTED-VERSIONS` | Positive | `CORE` | IsSupportedProtocolVersion validates supported protocol versions case-insensitively with whitespace trimming. | [`GatewayMetadataTests.cs:L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L8) | Backend xUnit |
| `CORE-GATEWAY-METADATA-UNSUPPORTED-VERSIONS` | **Guardrail** | `CORE` | IsSupportedProtocolVersion rejects unrecognized protocol versions. | [`GatewayMetadataTests.cs:L32`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L32) | Backend xUnit |
| `CORE-GATEWAY-METADATA-VERSION-NEGOTIATION` | Positive | `CORE` | NegotiateProtocolVersion canonicalizes casing for known protocol versions. | [`GatewayMetadataTests.cs:L41`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GatewayMetadataTests.cs#L41) | Backend xUnit |
| `DB-02` | Positive | `DB` | MSSQL stored procedure scripts declare all required procedures and parameter contracts correctly | [`DatabaseSchemaUpgradeAndContractTests.cs:L311`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L311) | Backend xUnit |
| `DB-07` | Positive | `DB` | SQLite upgrade migration automatically provisions OAuthClients table on legacy database | [`DatabaseSchemaUpgradeAndContractTests.cs:L433`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L433) | Backend xUnit |
| `DB-FACTORY-MSSQL-CONFIGURED` | Positive | `DB` | DbConnectionFactory initializes MS SQL Server database connection using Microsoft.Data.SqlClient. | [`DbConnectionFactoryTests.cs:L46`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L46) | Backend xUnit |
| `DB-FACTORY-MYSQL-CONFIGURED` | Positive | `DB` | DbConnectionFactory initializes MySQL database connection using MySqlConnector. | [`DbConnectionFactoryTests.cs:L28`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L28) | Backend xUnit |
| `DB-FACTORY-SQLITE-DEFAULT` | Positive | `DB` | DbConnectionFactory initializes SQLite database connection with SQLCipher encryption. | [`DbConnectionFactoryTests.cs:L10`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbConnectionFactoryTests.cs#L10) | Backend xUnit |
| `DB-INITIALIZER-CRUD-CONTRACT` | Positive | `DB` | DatabaseInitializer baseline schema supports CRUD operations across core domain tables. | [`DatabaseInitializerTests.cs:L71`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L71) | Backend xUnit |
| `DB-INITIALIZER-ENSURE-ALIAS` | Positive | `DB` | EnsureAliasColumn safely adds Alias column if missing and is idempotent. | [`DatabaseInitializerTests.cs:L54`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L54) | Backend xUnit |
| `DB-INITIALIZER-IDEMPOTENCY` | Positive | `DB` | DatabaseInitializer is idempotent and succeeds without error when executed multiple times. | [`DatabaseInitializerTests.cs:L40`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L40) | Backend xUnit |
| `DB-INITIALIZER-SCHEMA-BASELINE` | Positive | `DB` | DatabaseInitializer creates all 12 canonical tables on a fresh SQLite database. | [`DatabaseInitializerTests.cs:L10`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseInitializerTests.cs#L10) | Backend xUnit |
| `DB-MAPPING-SAVE-SQLITE` | Positive | `DB` | PermissionsController persists group mappings to SQLite database. | [`PermissionsControllerTests.cs:L182`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L182) | Backend xUnit |
| `DB-POLICY-SAVE-MYSQL` | Positive | `DB` | PermissionsController persists access policy to MySQL database repository. | [`PermissionsControllerTests.cs:L91`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L91) | Backend xUnit |
| `DB-POLICY-SAVE-SQLITE` | Positive | `DB` | PermissionsController persists access policy to SQLite database repository. | [`PermissionsControllerTests.cs:L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L80) | Backend xUnit |
| `DB-PROVIDER-INSTANTIATION-DIALECTS` | Positive | `DB` | DbConnectionFactory instantiates valid IDbConnection instances across sqlite, mysql, and mssql dialects. | [`MultiDatabaseProviderIntegrationTests.cs:L9`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L9) | Backend xUnit |
| `DB-QUOTA-REPO-DELETE` | Positive | `DB` | IUserQuotaRepository DeleteUserQuotaAsync removes user quota record | [`UserQuotaAndAppKeyRepositoryTests.cs:L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L132) | Backend xUnit |
| `DB-QUOTA-REPO-DI-REGISTRATION` | Positive | `DB` | IUserQuotaRepository is registered in dependency injection and resolvable | [`UserQuotaAndAppKeyRepositoryTests.cs:L199`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L199) | Backend xUnit |
| `DB-QUOTA-REPO-GET-ALL` | Positive | `DB` | IUserQuotaRepository GetAllUserQuotasAsync retrieves all quotas ordered by username | [`UserQuotaAndAppKeyRepositoryTests.cs:L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L98) | Backend xUnit |
| `DB-QUOTA-REPO-SET-GET` | Positive | `DB` | IUserQuotaRepository persists user quota overrides and retrieves them correctly | [`UserQuotaAndAppKeyRepositoryTests.cs:L85`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L85) | Backend xUnit |
| `DB-QUOTA-REPO-UPDATE-CONFLICT` | Positive | `DB` | IUserQuotaRepository SetUserQuotaAsync updates existing quota on conflict | [`UserQuotaAndAppKeyRepositoryTests.cs:L117`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UserQuotaAndAppKeyRepositoryTests.cs#L117) | Backend xUnit |
| `DB-SEEDER-INIT-SETTINGS-PROVIDERS` | Positive | `DB` | DatabaseSeederService initializes default router settings, provider configs, and schema. | [`DatabaseSeederServiceTests.cs:L30`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSeederServiceTests.cs#L30) | Backend xUnit |
| `DB-SEEDER-ROUTER-DEFAULT-DATA` | Positive | `DB` | DatabaseSeeder initializes default router tables, settings, and seed servers. | [`SeederAndDiscoveryTests.cs:L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L53) | Backend xUnit |
| `DB-SQLITE-LEGACY-UPGRADE-MIGRATION` | Positive | `DB` | SQLite auto-migration seamlessly upgrades legacy schema, encrypts plaintext secrets, and preserves data | [`DatabaseSchemaUpgradeAndContractTests.cs:L29`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseSchemaUpgradeAndContractTests.cs#L29) | Backend xUnit |
| `DB-TYPEHANDLER-JSON-LIST-SERIALIZATION` | Positive | `DB` | JsonListTypeHandler serializes and deserializes string collections to JSON text across database providers. | [`MultiDatabaseProviderIntegrationTests.cs:L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L42) | Backend xUnit |
| `SEC-DB-ENCRYPTION-KEY-AUTOGEN-FAIL` | **Guardrail** | `DB` | ResolveDbEncryptionKey wraps file persistence errors in InvalidOperationException | [`DbKeyHelperTests.cs:L334`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L334) | Backend xUnit |
| `DOC-SETUP-SKILL-FRONTMATTER` | Positive | `DOC` | mcg-setup skill frontmatter is valid YAML, specifies name, description starting with 'Use when...', and length is under 1024 characters | [`SetupSkillTests.cs:L18`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L18) | Backend xUnit |
| `DOC-SETUP-SKILL-MIRROR` | Positive | `DOC` | The mcg-setup skill and templates are mirrored 1:1 in .agents/skills/mcg-setup/ | [`SetupSkillTests.cs:L152`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L152) | Backend xUnit |
| `DOC-SETUP-SKILL-TEMPLATES` | Positive | `DOC` | All scaffold templates exist, are non-empty, and contain required directives such as responseBufferLimit, MCG_MASTER_KEY, and ghcr.io/spelech/model-context-gateway | [`SetupSkillTests.cs:L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L98) | Backend xUnit |
| `DOC-SETUP-SKILL-WORKFLOW` | Positive | `DOC` | mcg-setup skill contains all 6 required setup phases including environment probing, hosting platforms, env vs UI trade-offs, identity/network topology, artifact generation, and health/client configuration | [`SetupSkillTests.cs:L44`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SetupSkillTests.cs#L44) | Backend xUnit |
| `AUTH-EXTERNAL-IDP-DENIES-ANONYMOUS-LOOPBACK` | **Guardrail** | `GUARD` | When an external IDP is configured, anonymous loopback requests do not bypass authentication. | [`StandaloneAdminAuthTests.cs:L224`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L224) | Backend xUnit |
| `AUTH-STANDALONE-ADMINPOLICY-EXTERNAL-DENY` | **Guardrail** | `GUARD` | AdminPolicy rejects unauthenticated requests from non-whitelisted external IPs in standalone mode. | [`StandaloneAdminAuthTests.cs:L200`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L200) | Backend xUnit |
| `AUTH-STANDALONE-EXTERNAL-DENY` | **Guardrail** | `GUARD` | Standalone mode denies admin access to non-whitelisted external IPs without an Admin AppKey. | [`StandaloneAdminAuthTests.cs:L57`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StandaloneAdminAuthTests.cs#L57) | Backend xUnit |
| `GUARD-01` | **Guardrail** | `GUARD` | handles policy save failure with error toast | [`usePolicyStore.test.ts:L86`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/usePolicyStore.test.ts#L86) | Frontend Vitest |
| `GUARD-02` | **Guardrail** | `GUARD` | SSE transport fails closed with SecurityException when secret provider resolution fails | [`SseTransportTests.cs:L34`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L34) | Backend xUnit |
| `GUARD-03` | **Guardrail** | `GUARD` | CompositeSecretRetriever throws InvalidOperationException when an unregistered secret provider is requested. | [`CompositeSecretRetrieverTests.cs:L17`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CompositeSecretRetrieverTests.cs#L17) | Backend xUnit |
| `GUARD-04` | **Guardrail** | `GUARD` | Malformed completion payloads or unmapped backends must fail closed safely | [`PairwiseIntegrationMatrixTests.cs:L508`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L508) | Backend xUnit |
| `GUARD-05` | **Guardrail** | `GUARD` | Socket-level SSRF protection blocks private and loopback IP connections unless explicitly allowlisted. | [`ChallengerTests.cs:L712`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L712) | Backend xUnit |
| `GUARD-06` | **Guardrail** | `GUARD` | Auth middleware enforces case-insensitive route matching preventing path bypass. | [`ChallengerTests.cs:L216`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L216) | Backend xUnit |
| `GUARD-ADMIN-CUSTOM-FILES-VALIDATION` | **Guardrail** | `GUARD` | manage_custom_files rejects invalid prompt JSON syntax and unsupported file categories. | [`AdminToolsParityTests.cs:L779`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L779) | Backend xUnit |
| `GUARD-ADMIN-ENDPOINT-UNAUTHORIZED` | **Guardrail** | `GUARD` | Unauthenticated / non-admin client request to /admin receives 403 Forbidden. | [`AdminEndpointsTests.cs:L193`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L193) | Backend xUnit |
| `GUARD-ADMIN-POLICIES-WILDCARD-DENY` | **Guardrail** | `GUARD` | manage_policies rejects wildcard deny policies to prevent global lockout. | [`AdminToolsParityTests.cs:L513`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L513) | Backend xUnit |
| `GUARD-ADMIN-PROVIDERS-LDAP-PLAINTEXT` | **Guardrail** | `GUARD` | manage_providers rejects unencrypted LDAP connections on port 389. | [`AdminToolsParityTests.cs:L650`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L650) | Backend xUnit |
| `GUARD-ADMIN-SERVERS-VALIDATION` | **Guardrail** | `GUARD` | Verifies that the manage_servers tool accurately enforces validation by rejecting malformed transport types, missing required parameters, and requests for non-existent servers. | [`AdminToolsParityTests.cs:L324`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L324) | Backend xUnit |
| `GUARD-ADMIN-UNKNOWN-TOOL` | **Guardrail** | `GUARD` | AdminMcpServer returns an error response for unknown tool or action invocations. | [`AdminMcpServerTests.cs:L596`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L596) | Backend xUnit |
| `GUARD-APPKEY-EMPTY-CATEGORY` | **Guardrail** | `GUARD` | AppKey creation with empty or whitespace category must fail closed with BadRequest | [`CategoryScopedAppKeysTests.cs:L239`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L239) | Backend xUnit |
| `GUARD-APPKEY-MALFORMED-SCOPES` | **Guardrail** | `GUARD` | Corrupted AppKey scopes JSON must fail closed and reject execution | [`PairwiseIntegrationMatrixTests.cs:L490`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L490) | Backend xUnit |
| `GUARD-APPKEY-MISSING-NAME` | **Guardrail** | `GUARD` | Rejects AppKey creation with BadRequest when name is missing. | [`AppKeysControllerTests.cs:L312`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L312) | Backend xUnit |
| `GUARD-APPKEY-QUOTA-INVALID-PARAM` | **Guardrail** | `GUARD` | Returns BadRequest on invalid quota override input parameters. | [`AppKeysControllerTests.cs:L433`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L433) | Backend xUnit |
| `GUARD-APPKEY-REVOKE-FORBID` | **Guardrail** | `GUARD` | Returns Forbid when non-owner/non-admin attempts to revoke an AppKey. | [`AppKeysControllerTests.cs:L378`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L378) | Backend xUnit |
| `GUARD-APPKEY-REVOKE-NOTFOUND` | **Guardrail** | `GUARD` | Returns NotFound when revoking non-existent AppKey ID. | [`AppKeysControllerTests.cs:L369`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L369) | Backend xUnit |
| `GUARD-APPKEY-UNKNOWN-CATEGORY` | **Guardrail** | `GUARD` | Non-admin callers cannot create AppKeys with unconfigured categories | [`CategoryScopedAppKeysTests.cs:L220`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L220) | Backend xUnit |
| `GUARD-APPKEY-USER-QUOTA` | **Guardrail** | `GUARD` | Enforces user AppKey limit and returns BadRequest when quota is exceeded. | [`AppKeysControllerTests.cs:L324`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L324) | Backend xUnit |
| `GUARD-AUTH-MIDDLEWARE-UNAUTHORIZED` | **Guardrail** | `GUARD` | Auth middleware blocks unauthorized requests with HTTP 401 Unauthorized. | [`McpIntegrationTests.cs:L591`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L591) | Backend xUnit |
| `GUARD-AUTH-NULL-TARGET` | **Guardrail** | `GUARD` | Null or empty capability targets must immediately fail closed and return unauthorized | [`PairwiseIntegrationMatrixTests.cs:L468`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L468) | Backend xUnit |
| `GUARD-CLIENT-CLEANUP-REPO-ERR` | **Guardrail** | `GUARD` | CleanupClients returns 500 when repository throws. | [`ClientsControllerTests.cs:L352`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L352) | Backend xUnit |
| `GUARD-CLIENT-CREATE-REPO-ERR` | **Guardrail** | `GUARD` | CreateClient returns 500 when repository throws | [`ClientsControllerTests.cs:L300`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L300) | Backend xUnit |
| `GUARD-CLIENT-DELETE-REPO-ERR` | **Guardrail** | `GUARD` | DeleteClient returns 500 when repository throws | [`ClientsControllerTests.cs:L317`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L317) | Backend xUnit |
| `GUARD-CLIENT-EMPTY-CATEGORY` | **Guardrail** | `GUARD` | Client creation with empty category scope must fail closed with BadRequest | [`CategoryScopedAppKeysTests.cs:L304`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L304) | Backend xUnit |
| `GUARD-CLIENT-EMPTY-SCOPE` | **Guardrail** | `GUARD` | CreateClient fails closed when category scope is empty | [`ClientsControllerTests.cs:L283`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L283) | Backend xUnit |
| `GUARD-CLIENT-MISSING-NAME` | **Guardrail** | `GUARD` | CreateClient fails closed when DisplayName is missing | [`ClientsControllerTests.cs:L270`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientsControllerTests.cs#L270) | Backend xUnit |
| `GUARD-DISPOSED-01` | Positive | `GUARD` | Stateless HTTP POST lifecycle: HTTP response completes, HttpContext is marked disposed, background backend initialization completes successfully without ObjectDisposedException. | [`DownstreamSessionIntegrationTests.cs:L225`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L225) | Backend xUnit |
| `GUARD-HTTP-NO-SECRET-RETRIEVER` | **Guardrail** | `GUARD` | HTTP stateless transport fails closed with InvalidOperationException when no secret retriever is configured | [`HttpTransportTests.cs:L54`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L54) | Backend xUnit |
| `GUARD-HTTP-SECRET-RETRIEVAL-FAIL-CLOSED` | **Guardrail** | `GUARD` | HTTP stateless transport fails closed with SecurityException when secret resolution fails | [`HttpTransportTests.cs:L31`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L31) | Backend xUnit |
| `GUARD-LDAP-FILTER-ESCAPE` | Positive | `GUARD` | EscapeLdapFilter sanitizes and escapes special LDAP filter characters to prevent LDAP injection. | [`LdapActiveDirectoryServiceTests.cs:L10`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/LdapActiveDirectoryServiceTests.cs#L10) | Backend xUnit |
| `GUARD-MAPPING-GET-DB-ERROR` | **Guardrail** | `GUARD` | PermissionsController returns 500 on database error during mapping retrieval. | [`PermissionsControllerTests.cs:L147`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L147) | Backend xUnit |
| `GUARD-MAPPING-MISSING-EXT-ID` | **Guardrail** | `GUARD` | PermissionsController rejects mapping save missing external ID with BadRequest. | [`PermissionsControllerTests.cs:L160`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L160) | Backend xUnit |
| `GUARD-MAPPING-MISSING-GROUP` | **Guardrail** | `GUARD` | PermissionsController rejects mapping save missing internal group with BadRequest. | [`PermissionsControllerTests.cs:L171`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L171) | Backend xUnit |
| `GUARD-MAPPING-SAVE-DB-ERROR` | **Guardrail** | `GUARD` | PermissionsController returns 500 when saving mapping encounters DB error. | [`PermissionsControllerTests.cs:L193`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L193) | Backend xUnit |
| `GUARD-MAPPING-UNMAPPED-DENY` | **Guardrail** | `GUARD` | Fails closed and denies access when no valid group mapping exists for a restricted target. | [`GroupMappingsAndSpecAuthTests.cs:L125`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/GroupMappingsAndSpecAuthTests.cs#L125) | Backend xUnit |
| `GUARD-POLICY-DELETE-DB-ERROR` | **Guardrail** | `GUARD` | PermissionsController fails closed with 500 when database delete fails. | [`PermissionsControllerTests.cs:L124`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L124) | Backend xUnit |
| `GUARD-POLICY-MISSING-GROUP` | **Guardrail** | `GUARD` | PermissionsController rejects policy saves missing requiredGroup with BadRequest. | [`PermissionsControllerTests.cs:L69`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L69) | Backend xUnit |
| `GUARD-POLICY-MISSING-TARGET` | **Guardrail** | `GUARD` | PermissionsController rejects policy saves missing targetId with BadRequest. | [`PermissionsControllerTests.cs:L58`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PermissionsControllerTests.cs#L58) | Backend xUnit |
| `GUARD-PROVIDER-MISSING-AUTH-NAME` | **Guardrail** | `GUARD` | ProvidersController rejects saving auth provider without providerName with BadRequest. | [`ProvidersControllerTests.cs:L172`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L172) | Backend xUnit |
| `GUARD-PROVIDER-MISSING-SECRET-NAME` | **Guardrail** | `GUARD` | ProvidersController rejects saving secret provider without providerName with BadRequest. | [`ProvidersControllerTests.cs:L90`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProvidersControllerTests.cs#L90) | Backend xUnit |
| `GUARD-RBAC-COMPLETION-PROMPT` | **Guardrail** | `GUARD` | completion/complete throws UnauthorizedAccessException when caller lacks prompt permissions. | [`UnifiedMcpAuthorizationTests.cs:L613`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L613) | Backend xUnit |
| `GUARD-RBAC-COMPLETION-TEMPLATE` | **Guardrail** | `GUARD` | completion/complete throws UnauthorizedAccessException when caller lacks resource template permissions. | [`UnifiedMcpAuthorizationTests.cs:L645`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L645) | Backend xUnit |
| `GUARD-RBAC-COMPLETION-UNRESOLVED` | **Guardrail** | `GUARD` | completion/complete fails closed on unknown or unresolved completion references. | [`UnifiedMcpAuthorizationTests.cs:L677`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L677) | Backend xUnit |
| `GUARD-RBAC-DEFAULT-DENY` | **Guardrail** | `GUARD` | RBAC defaults to deny when no matching access policies are configured. | [`FineGrainedRbacTests.cs:L84`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L84) | Backend xUnit |
| `GUARD-RBAC-EXPLICIT-DENY` | **Guardrail** | `GUARD` | RBAC enforces explicit policy denials to reject unauthorized callers. | [`FineGrainedRbacTests.cs:L118`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L118) | Backend xUnit |
| `GUARD-RBAC-MISSING-GROUP` | **Guardrail** | `GUARD` | RBAC denies access when user does not possess the required security group. | [`FineGrainedRbacTests.cs:L106`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L106) | Backend xUnit |
| `GUARD-RBAC-NULL-TARGET` | **Guardrail** | `GUARD` | IsUserAuthorizedAsync fails closed on null, empty, or whitespace target identifiers. | [`UnifiedMcpAuthorizationTests.cs:L198`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L198) | Backend xUnit |
| `GUARD-RBAC-PROMPT-UNAUTHORIZED` | **Guardrail** | `GUARD` | GetPromptAsync throws UnauthorizedAccessException when user lacks permissions for target prompt. | [`FineGrainedRbacTests.cs:L145`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L145) | Backend xUnit |
| `GUARD-RBAC-RESOURCE-UNAUTHORIZED` | **Guardrail** | `GUARD` | ReadResourceAsync throws UnauthorizedAccessException when user lacks permissions for target resource. | [`FineGrainedRbacTests.cs:L159`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L159) | Backend xUnit |
| `GUARD-RBAC-TOOL-UNAUTHORIZED` | **Guardrail** | `GUARD` | CallToolAsync returns a formatted security error when user is unauthorized. | [`FineGrainedRbacTests.cs:L130`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/FineGrainedRbacTests.cs#L130) | Backend xUnit |
| `GUARD-ROUTING-UNKNOWN-TOOL` | **Guardrail** | `GUARD` | ToolRoutingManager throws KeyNotFoundException when calling a tool not registered in the routing table. | [`ToolRoutingManagerTests.cs:L187`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L187) | Backend xUnit |
| `GUARD-ROUTING-UNREGISTERED-RESOURCE` | **Guardrail** | `GUARD` | ResourceRoutingManager throws KeyNotFoundException when reading an unregistered resource URI. | [`ResourceRoutingManagerTests.cs:L67`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L67) | Backend xUnit |
| `GUARD-SECURITY-IDENTIFIER-VALIDATION` | Positive | `GUARD` | SecurityValidationHelper validates tool and prompt names against namespaced server identifiers. | [`SecurityValidationHelperTests.cs:L44`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecurityValidationHelperTests.cs#L44) | Backend xUnit |
| `GUARD-STATE-DISCONNECT-CANCELLATION` | **Guardrail** | `GUARD` | JsonRpcStateManager rejects registration and cancels pending completions upon disconnect. | [`ConcurrentResponseIsolationTests.cs:L578`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L578) | Backend xUnit |
| `GUARD-TOOL-CANCELLATION` | **Guardrail** | `GUARD` | ToolRoutingManager propagates task cancellation gracefully with a standardized JSON-RPC error response. | [`ToolRoutingManagerTests.cs:L155`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L155) | Backend xUnit |
| `GUARD-TOOL-MANDATORY-PARAMS` | **Guardrail** | `GUARD` | ToolRoutingManager returns an error when execute_tool is invoked without the mandatory tool name parameter. | [`ToolRoutingManagerTests.cs:L125`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L125) | Backend xUnit |
| `GUARD-UNSUPPORTED-DB-PROVIDER` | **Guardrail** | `GUARD` | DbConnectionFactory fails closed and throws InvalidOperationException when configured with unsupported database provider. | [`MultiDatabaseProviderIntegrationTests.cs:L29`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MultiDatabaseProviderIntegrationTests.cs#L29) | Backend xUnit |
| `GUARD-VALIDATION-STDIO-SHELL-OPERATORS` | **Guardrail** | `GUARD` | ServerValidationHelper validates stdio commands against unsafe shell operators, piping, and command injection. | [`ServerEndpointsValidationTests.cs:L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L7) | Backend xUnit |
| `MCP-ADMIN-TOOL-TEST-CALL-ERROR` | **Guardrail** | `GUARD` | AdminMcpServer test_tool_call propagates downstream backend errors with visibility. | [`AdminMcpServerTests.cs:L637`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L637) | Backend xUnit |
| `API-PIPELINE-GET-HEALTH` | Positive | `MCP` | GET /health returns gateway health status with 200 OK. | [`PipelineIntegrationTests.cs:L438`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L438) | Backend xUnit |
| `API-PIPELINE-GET-SERVERS` | Positive | `MCP` | GET /api/servers returns backend servers list with 200 OK. | [`PipelineIntegrationTests.cs:L339`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L339) | Backend xUnit |
| `API-PIPELINE-GET-STATS` | Positive | `MCP` | GET /api/stats returns server statistics with 200 OK. | [`PipelineIntegrationTests.cs:L429`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L429) | Backend xUnit |
| `API-PIPELINE-GET-VERSION` | Positive | `MCP` | GET /api/version returns version information with 200 OK. | [`PipelineIntegrationTests.cs:L330`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L330) | Backend xUnit |
| `API-PIPELINE-POST-MESSAGE-PROTOCOL` | Positive | `MCP` | Full end-to-end JSON-RPC session message suite executes over HTTP POST. | [`PipelineIntegrationTests.cs:L142`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L142) | Backend xUnit |
| `API-PIPELINE-POST-SSE-PROTOCOL` | Positive | `MCP` | Full end-to-end JSON-RPC protocol suite executes across SSE pipeline. | [`PipelineIntegrationTests.cs:L85`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L85) | Backend xUnit |
| `API-PIPELINE-SERVER-CRUD` | Positive | `MCP` | Backend server CRUD pipeline endpoints persist and manage downstream servers. | [`PipelineIntegrationTests.cs:L276`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L276) | Backend xUnit |
| `HEALTH-PROBE-ALL-ENABLED-FLEET` | Positive | `MCP` | BackendHealthCheckService probes all enabled backend servers in the fleet. | [`BackendHealthCheckServiceTests.cs:L161`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L161) | Backend xUnit |
| `HEALTH-PROBE-CUSTOM-SERVER-CONNECTED` | Positive | `MCP` | BackendHealthCheckService marks registered custom servers as Connected. | [`BackendHealthCheckServiceTests.cs:L264`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L264) | Backend xUnit |
| `HEALTH-PROBE-DISABLED-SERVER` | Positive | `MCP` | BackendHealthCheckService marks disabled servers as Disabled. | [`BackendHealthCheckServiceTests.cs:L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L132) | Backend xUnit |
| `HEALTH-PROBE-HTTP-CONNECTED-200` | Positive | `MCP` | BackendHealthCheckService marks server as Connected when downstream HTTP/SSE endpoint returns 200 OK. | [`BackendHealthCheckServiceTests.cs:L57`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L57) | Backend xUnit |
| `HEALTH-PROBE-HTTP-FAILED-EXCEPTION` | Positive | `MCP` | BackendHealthCheckService marks server as Failed when downstream HTTP connection fails. | [`BackendHealthCheckServiceTests.cs:L97`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L97) | Backend xUnit |
| `HEALTH-PROBE-STDIO-VALID-CONNECTED` | Positive | `MCP` | BackendHealthCheckService sets valid STDIO servers to Connected without making network HTTP probes. | [`BackendHealthCheckServiceTests.cs:L190`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/BackendHealthCheckServiceTests.cs#L190) | Backend xUnit |
| `MCP-01` | Positive | `MCP` | initializes with default state | [`useServerStore.test.ts:L24`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useServerStore.test.ts#L24) | Frontend Vitest |
| `MCP-02` | Positive | `MCP` | All MCP protocol capabilities enforce caller role authorizations consistently | [`PairwiseIntegrationMatrixTests.cs:L385`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L385) | Backend xUnit |
| `MCP-05` | Positive | `MCP` | ResourceRoutingManager returns all registered resources when search query is empty. | [`ResourceRoutingManagerTests.cs:L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ResourceRoutingManagerTests.cs#L8) | Backend xUnit |
| `MCP-06` | Positive | `MCP` | prompts/list aggregates, namespaces, and routes prompts to target backends. | [`McpIntegrationTests.cs:L499`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L499) | Backend xUnit |
| `MCP-08` | Positive | `MCP` | completion/complete forwards prompt completions to backend when caller is authorized. | [`UnifiedMcpAuthorizationTests.cs:L473`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L473) | Backend xUnit |
| `MCP-10` | Positive | `MCP` | DockerAutoDiscoveryService handles missing Docker socket gracefully without throwing unhandled exceptions. | [`SeederAndDiscoveryTests.cs:L83`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SeederAndDiscoveryTests.cs#L83) | Backend xUnit |
| `MCP-12` | Positive | `MCP` | DynamicEmbeddingService retrieves and persists embedding provider configurations in Settings table. | [`DynamicEmbeddingServiceTests.cs:L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DynamicEmbeddingServiceTests.cs#L62) | Backend xUnit |
| `MCP-15` | Positive | `MCP` | All JSON-RPC results return a resultType discriminator (complete or input_required) per MCP 2026-07-28 spec. | [`ProtocolResultTypeTests.cs:L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProtocolResultTypeTests.cs#L7) | Backend xUnit |
| `MCP-21` | Positive | `MCP` | Admin endpoint handles direct Streamable HTTP POST tools/list request returning JSON even with Accept text/event-stream header. | [`AdminEndpointsTests.cs:L370`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L370) | Backend xUnit |
| `MCP-22` | **Guardrail** | `MCP` | AdminMcpServer ProcessRequestAsync handles server/discover request returning supported versions and subscriptions capability. | [`AdminMcpServerTests.cs:L686`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L686) | Backend xUnit |
| `MCP-23` | Positive | `MCP` | AdminMcpServer HandleInitializeAsync includes subscriptions capability in capabilities object. | [`AdminMcpServerTests.cs:L655`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L655) | Backend xUnit |
| `MCP-24` | Positive | `MCP` | McpSpecMiddleware extracts OpenTelemetry W3C traceparent, tracestate, and baggage from headers and _meta. | [`McpSpecMiddlewareTests.cs:L219`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpSpecMiddlewareTests.cs#L219) | Backend xUnit |
| `MCP-25` | Positive | `MCP` | ToolRoutingManager falls back to SessionManager global server tools cache during cold-start search_tools execution | [`ToolRoutingManagerTests.cs:L264`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L264) | Backend xUnit |
| `MCP-26` | Positive | `MCP` | ToolRoutingManager normalizes tool name delimiters (slash and colon) to canonical double-underscore format. | [`ToolRoutingManagerTests.cs:L319`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L319) | Backend xUnit |
| `MCP-27` | Positive | `MCP` | McpServer supports Alias property | [`McpServerTests.cs:L15`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpServerTests.cs#L15) | Backend xUnit |
| `MCP-28` | **Guardrail** | `MCP` | ToolRoutingManager rejects ambiguous bare tool calls when duplicate tool names exist across distinct servers, listing candidates with namespaces. | [`ToolRoutingManagerTests.cs:L489`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolRoutingManagerTests.cs#L489) | Backend xUnit |
| `MCP-29` | **Guardrail** | `MCP` | ServerValidationHelper rejects invalid characters in Alias. | [`ServerEndpointsValidationTests.cs:L72`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L72) | Backend xUnit |
| `MCP-30` | Positive | `MCP` | IsUserAuthorizedAsync matches granular tool policies across /, :, and __ delimiters. | [`UnifiedMcpAuthorizationTests.cs:L284`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/UnifiedMcpAuthorizationTests.cs#L284) | Backend xUnit |
| `MCP-31` | **Guardrail** | `MCP` | DockerAutoDiscoveryService parses mcp.alias from Docker container labels | [`DockerAutoDiscoveryServiceTests.cs:L188`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DockerAutoDiscoveryServiceTests.cs#L188) | Backend xUnit |
| `MCP-ADMIN-ENDPOINT-CALL-TOOL` | Positive | `MCP` | Admin endpoint /admin/message executes tools/call for manage_system diagnostics. | [`AdminEndpointsTests.cs:L294`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L294) | Backend xUnit |
| `MCP-ADMIN-ENDPOINT-HEAD-REQUEST` | Positive | `MCP` | Admin endpoint /admin handles HEAD request returning text/event-stream headers. | [`AdminEndpointsTests.cs:L212`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L212) | Backend xUnit |
| `MCP-ADMIN-ENDPOINT-LIST-TOOLS` | Positive | `MCP` | Admin endpoint /admin/message executes tools/list over active SSE session and returns 10 admin tools. | [`AdminEndpointsTests.cs:L224`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L224) | Backend xUnit |
| `MCP-ADMIN-ENDPOINT-ROUTER-ADMIN-TARGET` | Positive | `MCP` | Target proxy endpoint /router-admin routes directly to the Admin MCP server. | [`AdminEndpointsTests.cs:L149`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L149) | Backend xUnit |
| `MCP-ADMIN-ENDPOINT-SSE-HANDSHAKE` | Positive | `MCP` | Admin endpoint /admin/sse performs initialize handshake with 2026-07-28 protocol version. | [`AdminEndpointsTests.cs:L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L62) | Backend xUnit |
| `MCP-ADMIN-INITIALIZE-HANDSHAKE` | Positive | `MCP` | AdminMcpServer initialize handles protocol negotiation for 2026-07-28 and 2024-11-05. | [`AdminMcpServerTests.cs:L199`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L199) | Backend xUnit |
| `MCP-ADMIN-PARITY-APPKEYS` | Positive | `MCP` | manage_appkeys supports full parity for list, get_limits, create, and revoke actions. | [`AdminToolsParityTests.cs:L370`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L370) | Backend xUnit |
| `MCP-ADMIN-PARITY-CLIENTS` | Positive | `MCP` | manage_clients supports full parity for register, list, and delete actions. | [`AdminToolsParityTests.cs:L423`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L423) | Backend xUnit |
| `MCP-ADMIN-PARITY-CUSTOM-FILES` | Positive | `MCP` | manage_custom_files supports full parity for list, get, save, and delete prompt and resource files. | [`AdminToolsParityTests.cs:L716`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L716) | Backend xUnit |
| `MCP-ADMIN-PARITY-GROUP-MAPPINGS` | Positive | `MCP` | manage_group_mappings supports full parity for list, save, and delete external-to-internal group mappings. | [`AdminToolsParityTests.cs:L530`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L530) | Backend xUnit |
| `MCP-ADMIN-PARITY-JSONRPC-DISPATCH` | Positive | `MCP` | AdminMcpServer processes standard JSON-RPC 2.0 requests (tools/list, tools/call, ping). | [`AdminToolsParityTests.cs:L873`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L873) | Backend xUnit |
| `MCP-ADMIN-PARITY-POLICIES` | Positive | `MCP` | manage_policies supports full parity for list, save, and delete access control policies. | [`AdminToolsParityTests.cs:L464`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L464) | Backend xUnit |
| `MCP-ADMIN-PARITY-PROVIDERS` | Positive | `MCP` | manage_providers supports full parity for list, save_secret, test_vault, save_auth, and test_ldap actions. | [`AdminToolsParityTests.cs:L577`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L577) | Backend xUnit |
| `MCP-ADMIN-PARITY-SERVER-ALIAS` | Positive | `MCP` | AdminMcpServer manage_servers supports server alias for add, update, and list actions with collision validation. | [`AdminMcpServerTests.cs:L858`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L858) | Backend xUnit |
| `MCP-ADMIN-PARITY-SERVERS` | Positive | `MCP` | Validates that the manage_servers tool provides comprehensive administrative capabilities including listing, retrieving, creating, updating, toggling, deleting, and reconnecting servers. | [`AdminToolsParityTests.cs:L234`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L234) | Backend xUnit |
| `MCP-ADMIN-PARITY-SETTINGS` | Positive | `MCP` | manage_settings supports full parity for get and update global router configurations. | [`AdminToolsParityTests.cs:L667`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L667) | Backend xUnit |
| `MCP-ADMIN-PARITY-SYSTEM` | Positive | `MCP` | manage_system supports full parity for diagnostics, get_logs, clear_logs, and query_audit actions. | [`AdminToolsParityTests.cs:L816`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L816) | Backend xUnit |
| `MCP-ADMIN-PARITY-TEST-TOOL-CALL` | Positive | `MCP` | test_tool_call executes test bench backend tool calls and formats responses. | [`AdminToolsParityTests.cs:L846`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L846) | Backend xUnit |
| `MCP-ADMIN-PARITY-TOOLS-COVERAGE` | Positive | `MCP` | Ensures every UI management workflow is backed by a verified, equivalent action within the consolidated Admin MCP tools. | [`AdminToolsParityTests.cs:L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminToolsParityTests.cs#L191) | Backend xUnit |
| `MCP-ADMIN-RECONNECT-ALL-PROPAGATION` | Positive | `MCP` | AdminMcpServer manage_servers reconnect_all triggers StartInitializationForBackend across active sessions. | [`AdminMcpServerTests.cs:L730`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L730) | Backend xUnit |
| `MCP-ADMIN-REQUEST-WITHOUT-ID-NOT-DROPPED` | Positive | `MCP` | Admin endpoint executes requests without an ID and does not drop them as notifications. | [`AdminEndpointsTests.cs:L550`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L550) | Backend xUnit |
| `MCP-ADMIN-REVERSE-PROXY-X-FORWARDED-HOST` | Positive | `MCP` | Admin SSE endpoint prioritizes X-Forwarded-Host header over Request.Host in endpoint URI advertising. | [`AdminEndpointsTests.cs:L514`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L514) | Backend xUnit |
| `MCP-ADMIN-SKILL-E2E-PROVISIONING` | Positive | `MCP` | Admin automation templates and JSON-RPC tool calls successfully provision a blank-slate gateway instance end-to-end via HTTP /admin/message. | [`AdminAutomationSkillTests.cs:L176`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L176) | Backend xUnit |
| `MCP-ADMIN-SKILL-FRONTMATTER` | Positive | `MCP` | mcg-admin skill frontmatter is valid YAML, specifies name, description starting with 'Use when...', and length is under 1024 characters | [`AdminAutomationSkillTests.cs:L21`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L21) | Backend xUnit |
| `MCP-ADMIN-SKILL-MIRROR` | Positive | `MCP` | mcg-admin skill files and templates are identically mirrored between skills/ and .agents/skills/ directories | [`AdminAutomationSkillTests.cs:L147`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L147) | Backend xUnit |
| `MCP-ADMIN-SKILL-TEMPLATES` | Positive | `MCP` | All mcg-admin scaffold templates exist, are non-empty, and contain valid JSON or scripts for Authentik, Keycloak, Entra, ActiveDirectory, Cloudflare, Vault, Embeddings, Docker, and shell automation | [`AdminAutomationSkillTests.cs:L103`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L103) | Backend xUnit |
| `MCP-ADMIN-SKILL-WORKFLOW` | Positive | `MCP` | mcg-admin skill contains all 7 administration phases including diagnostics, secrets, auth providers, RBAC/group mappings, settings/embeddings, servers/clients, and live tool verification | [`AdminAutomationSkillTests.cs:L47`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L47) | Backend xUnit |
| `MCP-ADMIN-SSE-ZOD-COMPLIANT` | Positive | `MCP` | Admin endpoint direct POST response does not serialize null error or _meta fields. | [`AdminEndpointsTests.cs:L487`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L487) | Backend xUnit |
| `MCP-ADMIN-TOOL-AUDIT-LOG` | Positive | `MCP` | AdminMcpServer tool calls record audit log entries with caller and tool name. | [`AdminMcpServerTests.cs:L270`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L270) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-APPKEYS` | Positive | `MCP` | AdminMcpServer executes manage_appkeys create, list, limits, and revoke actions. | [`AdminMcpServerTests.cs:L287`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L287) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-CLIENTS` | Positive | `MCP` | AdminMcpServer executes manage_clients register, list, and delete actions. | [`AdminMcpServerTests.cs:L334`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L334) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-CUSTOM-FILES` | Positive | `MCP` | AdminMcpServer executes manage_custom_files save, get, list, and delete actions. | [`AdminMcpServerTests.cs:L508`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L508) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-GROUP-MAPPINGS` | Positive | `MCP` | AdminMcpServer executes manage_group_mappings save, list, and delete actions. | [`AdminMcpServerTests.cs:L406`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L406) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-POLICIES` | Positive | `MCP` | AdminMcpServer executes manage_policies save, list, and delete actions. | [`AdminMcpServerTests.cs:L372`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L372) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-PROVIDERS` | Positive | `MCP` | AdminMcpServer executes manage_providers list, save_secret, and save_auth actions. | [`AdminMcpServerTests.cs:L439`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L439) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-SERVERS` | Positive | `MCP` | AdminMcpServer executes manage_servers list, get, create, update, toggle, and delete actions. | [`AdminMcpServerTests.cs:L218`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L218) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-SETTINGS` | Positive | `MCP` | AdminMcpServer executes manage_settings get and update actions. | [`AdminMcpServerTests.cs:L482`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L482) | Backend xUnit |
| `MCP-ADMIN-TOOL-MANAGE-SYSTEM` | Positive | `MCP` | AdminMcpServer executes manage_system diagnostics, get_logs, clear_logs, and query_audit actions. | [`AdminMcpServerTests.cs:L562`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L562) | Backend xUnit |
| `MCP-ADMIN-TOOLS-LIST-COUNT` | Positive | `MCP` | AdminMcpServer tools/list returns all 10 consolidated tools with complete JSON schemas. | [`AdminMcpServerTests.cs:L151`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L151) | Backend xUnit |
| `MCP-ADMIN-ZOD-STRICT-RESPONSE` | Positive | `MCP` | JsonRpcResponse serialization omits null result, error, and _meta fields completely to satisfy Zod strict validation. | [`AdminEndpointsTests.cs:L452`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminEndpointsTests.cs#L452) | Backend xUnit |
| `MCP-COLDSTART-01` | Positive | `MCP` | Full cold-start cycle: SessionManager cache seeded -> search_tools -> execute_tool dispatches to downstream mock server and returns output. | [`DownstreamSessionIntegrationTests.cs:L130`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L130) | Backend xUnit |
| `MCP-DOWNSTREAM-INIT-DIAGNOSTICS` | Positive | `MCP` | Initializes downstream MCP backends with detailed diagnostic logging. | [`McpIntegrationTests.cs:L317`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L317) | Backend xUnit |
| `MCP-ERROR-ACTIONABLE-SUGGESTION-CATEGORIES` | Positive | `MCP` | ToolErrorFormatter categorizes error messages and produces domain-specific remediation guidance. | [`ToolErrorFormatterTests.cs:L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L42) | Backend xUnit |
| `MCP-ERROR-FORMAT-JSONRPC-REMEDIATION` | Positive | `MCP` | ToolErrorFormatter attaches actionable suggestions and remediation resource URIs to JSON-RPC error payloads. | [`ToolErrorFormatterTests.cs:L7`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L7) | Backend xUnit |
| `MCP-ERROR-FORMAT-UNHANDLED-EXCEPTION` | Positive | `MCP` | ToolErrorFormatter converts unhandled exceptions to standardized JSON-RPC error format with suggestions. | [`ToolErrorFormatterTests.cs:L27`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ToolErrorFormatterTests.cs#L27) | Backend xUnit |
| `MCP-EXEC-TOOL-ENFORCE-AUTH-POLICIES` | Positive | `MCP` | Meta-mode execute_tool strictly enforces target tool authorization policies | [`PairwiseIntegrationMatrixTests.cs:L567`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PairwiseIntegrationMatrixTests.cs#L567) | Backend xUnit |
| `MCP-EXEC-TOOL-ENFORCE-CATEGORY-SCOPES` | Positive | `MCP` | Router meta-mode execute_tool validates and enforces category scopes on target tool calls | [`CategoryScopedAppKeysTests.cs:L428`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/CategoryScopedAppKeysTests.cs#L428) | Backend xUnit |
| `MCP-FILES-DIR-HELPER-INIT` | Positive | `MCP` | CustomFilesDirectoryHelper initializes and creates required directories on startup. | [`McpIntegrationTests.cs:L1069`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L1069) | Backend xUnit |
| `MCP-JSON-REWRITE-ADVERSARIAL-EDGE-CASES` | Positive | `MCP` | JsonNode request rewrite handles adversarial mixed arrays, block comments, and malformed JSON. | [`ChallengerTests.cs:L572`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L572) | Backend xUnit |
| `MCP-JSON-REWRITE-BATCH-COMMENTS-COMMAS` | Positive | `MCP` | RewriteRequestJson accurately parses JSON batches, comments, and trailing commas using System.Text.Json JsonNode. | [`ChallengerTests.cs:L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L191) | Backend xUnit |
| `MCP-JSONRPC-CONVERTER-MINIMAL-VARIANTS` | Positive | `MCP` | JsonRpcMessageConverter deserializes edge-case minimal/null JSON-RPC variants without stack overflows. | [`ChallengerTests.cs:L684`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L684) | Backend xUnit |
| `MCP-JSONRPC-CONVERTER-PRIORITIZE-RESPONSE` | Positive | `MCP` | JsonRpcMessageConverter prioritizes result/error properties over method property in polymorphic response parsing. | [`ChallengerTests.cs:L336`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L336) | Backend xUnit |
| `MCP-JSONRPC-DESERIALIZE-PLAIN-NO-OVERFLOW` | Positive | `MCP` | Deserializing plain JsonRpcMessage does not cause recursive converter invocation or stack overflow. | [`McpIntegrationTests.cs:L287`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L287) | Backend xUnit |
| `MCP-JSONRPC-POLYMORPHIC-DESERIALIZATION` | Positive | `MCP` | Polymorphic JSON-RPC message deserializer accurately instantiates request, response, and notification subclasses. | [`McpIntegrationTests.cs:L259`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L259) | Backend xUnit |
| `MCP-JSONRPC-SERIALIZE-PLAIN-NO-OVERFLOW` | Positive | `MCP` | Serializing plain JsonRpcMessage does not cause recursive converter invocation or stack overflow. | [`McpIntegrationTests.cs:L303`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L303) | Backend xUnit |
| `MCP-MOCK-JSONRPC-FLOW` | Positive | `MCP` | MockDownstreamMcpServer handles initialize, initialized, tools/list, and tools/call JSON-RPC 2.0 protocol cycles. | [`MockDownstreamMcpServerTests.cs:L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L11) | Backend xUnit |
| `MCP-MOCK-RESOURCES-PROMPTS-SUPPORT` | Positive | `MCP` | MockDownstreamMcpServer handles resources/list and prompts/list MCP protocol methods. | [`TransportResilienceTests.cs:L123`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L123) | Backend xUnit |
| `MCP-RESILIENT-01` | Positive | `MCP` | Prefix-based resilient routing: execute_tool called with unregistered but prefixed tool name dynamically resolves server and executes. | [`DownstreamSessionIntegrationTests.cs:L294`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DownstreamSessionIntegrationTests.cs#L294) | Backend xUnit |
| `MCP-SESSION-BUILTIN-PROMPTS` | Positive | `MCP` | ClientSession lists built-in diagnostic and routing prompts. | [`ClientSessionTests.cs:L115`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L115) | Backend xUnit |
| `MCP-SESSION-BUILTIN-RESOURCES` | Positive | `MCP` | ClientSession lists built-in resources including router://status. | [`ClientSessionTests.cs:L99`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L99) | Backend xUnit |
| `MCP-SESSION-CALL-SEARCH-TOOLS` | Positive | `MCP` | ClientSession calls built-in search_tools and returns structured search results. | [`ClientSessionTests.cs:L132`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L132) | Backend xUnit |
| `MCP-SESSION-ERROR-TRANSFORM-CANCEL-SAMPLING` | Positive | `MCP` | Translates backend error codes, handles cancellation tokens, and executes sampling requests. | [`McpIntegrationTests.cs:L880`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L880) | Backend xUnit |
| `MCP-SESSION-GET-PROMPT-DIAGNOSE` | Positive | `MCP` | ClientSession gets router__diagnose_failure prompt and returns diagnostic prompt instructions. | [`ClientSessionTests.cs:L176`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L176) | Backend xUnit |
| `MCP-SESSION-MANAGER-PER-SERVER-CACHE` | Positive | `MCP` | SessionManager caches and isolates connections per downstream backend server. | [`McpIntegrationTests.cs:L1084`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L1084) | Backend xUnit |
| `MCP-SESSION-METAMODE-TOOLS` | Positive | `MCP` | ClientSession exposes meta-mode tools search_tools and execute_tool in meta mode. | [`ClientSessionTests.cs:L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L80) | Backend xUnit |
| `MCP-SESSION-READ-ROUTER-STATUS` | Positive | `MCP` | ClientSession reads router://status resource and returns online gateway metadata. | [`ClientSessionTests.cs:L153`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L153) | Backend xUnit |
| `MCP-SESSION-REQUEST-CANCELLATION` | Positive | `MCP` | ClientSession registers and triggers request cancellation for active cancellation tokens. | [`ClientSessionTests.cs:L198`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L198) | Backend xUnit |
| `MCP-SESSION-UNREGISTERED-RESPONSE` | Positive | `MCP` | ClientSession returns false when handling client response for unregistered request ID. | [`ClientSessionTests.cs:L214`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ClientSessionTests.cs#L214) | Backend xUnit |
| `MCP-WIRE-JSONRPC-SPEC` | Positive | `MCP` | MockDownstreamMcpServer treats messages with omitted id as notifications returning 202 Accepted with empty body. | [`MockDownstreamMcpServerTests.cs:L98`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L98) | Backend xUnit |
| `MCP-WIRE-PROTOCOL-MATRIX` | Positive | `MCP` | MockDownstreamMcpServer dynamically negotiates requested protocol version. | [`MockDownstreamMcpServerTests.cs:L80`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MockDownstreamMcpServerTests.cs#L80) | Backend xUnit |
| `UI-104` | Positive | `MCP` | renders resource tester with servers and resources | [`ResourceTesterCard.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ResourceTesterCard.test.tsx#L1) | Frontend Vitest |
| `UI-106` | Positive | `MCP` | renders connected server details with badges and triggers actions | [`ServerCard.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L1) | Frontend Vitest |
| `UI-107` | Positive | `MCP` | renders prompt dropdown and filters by selected server | [`PromptTesterCard.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/PromptTesterCard.test.tsx#L1) | Frontend Vitest |
| `UI-112` | Positive | `MCP` | renders nothing when isAddEditOpen is false | [`ServerModal.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerModal.test.tsx#L1) | Frontend Vitest |
| `UI-121` | Positive | `MCP` | should open Add Server modal and switch secret provider types | [`server-management.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/server-management.spec.ts#L1) | Playwright E2E |
| `UI-126` | Positive | `MCP` | should open Server Inspect Modal if servers are present on dashboard | [`server-inspector.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/server-inspector.spec.ts#L1) | Playwright E2E |
| `UI-SERVERS-ALIAS-MANAGEMENT` | Positive | `MCP` | renders server alias badge alongside server id when configured | [`ServerCard.test.tsx:L145`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerCard.test.tsx#L145) | Frontend Vitest |
| `API-PIPELINE-GET-AUDIT` | Positive | `SEC` | GET /api/audit returns audit log records with 200 OK. | [`PipelineIntegrationTests.cs:L393`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L393) | Backend xUnit |
| `API-PIPELINE-GET-LOGS` | Positive | `SEC` | GET /api/logs returns system log records with 200 OK. | [`PipelineIntegrationTests.cs:L420`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L420) | Backend xUnit |
| `AUTH-106` | **Guardrail** | `SEC` | Exchange throws InvalidOperationException when request is null. | [`AuthorizationControllerTests.cs:L19`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L19) | Backend xUnit |
| `AUTH-107` | Positive | `SEC` | RegisterClient successfully handles DCR requests when open DCR is enabled. | [`AuthorizationControllerTests.cs:L36`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L36) | Backend xUnit |
| `AUTH-108` | **Guardrail** | `SEC` | Authorize throws InvalidOperationException when OIDC request is null. | [`AuthorizationControllerTests.cs:L77`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L77) | Backend xUnit |
| `AUTH-109` | Positive | `SEC` | RegisterClient uses IOAuthClientRepository when IOpenIddictApplicationManager is null. | [`AuthorizationControllerTests.cs:L94`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L94) | Backend xUnit |
| `AUTH-111` | **Guardrail** | `SEC` | Pipeline exposes RFC 9728 OAuth Protected Resource discovery endpoints with dynamic resource identifiers. | [`PipelineIntegrationTests.cs:L62`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L62) | Backend xUnit |
| `AUTH-112` | **Guardrail** | `SEC` | Authorize resolves client application from IOAuthClientRepository and redirects to consent. | [`AuthorizationControllerTests.cs:L297`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L297) | Backend xUnit |
| `AUTH-113` | Positive | `SEC` | RegisterClient supports public clients with PKCE (token_endpoint_auth_method: none) and omits client secret. | [`AuthorizationControllerTests.cs:L351`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L351) | Backend xUnit |
| `AUTH-114` | **Guardrail** | `SEC` | RegisterClient rejects invalid or non-absolute redirect URIs with standard RFC 7591 invalid_redirect_uri error. | [`AuthorizationControllerTests.cs:L400`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L400) | Backend xUnit |
| `AUTH-115` | Positive | `SEC` | RegisterClient dynamically binds requested scopes to OpenIddict application descriptor permissions. | [`AuthorizationControllerTests.cs:L435`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L435) | Backend xUnit |
| `AUTH-116` | **Guardrail** | `SEC` | Exchange rejects client_credentials grant attempts by public clients with UnauthorizedClient error. | [`AuthorizationControllerTests.cs:L475`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L475) | Backend xUnit |
| `AUTH-117` | **Guardrail** | `SEC` | RegisterClient returns 403 Forbidden with access_denied when open client registration is disabled and caller is unauthorized. | [`AuthorizationControllerTests.cs:L516`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuthorizationControllerTests.cs#L516) | Backend xUnit |
| `MCP-ADMIN-TEST-TOOL-CALL-SECRET-RESOLUTION` | Positive | `SEC` | AdminMcpServer test_tool_call resolves server secrets via injected ISecretRetriever. | [`AdminMcpServerTests.cs:L765`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L765) | Backend xUnit |
| `SEC-01` | **Guardrail** | `SEC` | SQLite database is encrypted at rest using SQLCipher with DB_ENCRYPTION_KEY. | [`DatabaseEncryptionTests.cs:L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DatabaseEncryptionTests.cs#L8) | Backend xUnit |
| `SEC-02` | Positive | `SEC` | VaultSecretRetriever dynamically loads, applies, and reloads Vault configurations from database repository. | [`ProviderSettingsEncryptionTests.cs:L294`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L294) | Backend xUnit |
| `SEC-03` | Positive | `SEC` | EnvironmentSecretRetriever retrieves configured environment variable value. | [`SecretRetrieverTests.cs:L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecretRetrieverTests.cs#L5) | Backend xUnit |
| `SEC-04` | Positive | `SEC` | WindowsRegistrySecretRetriever handles non-Windows platforms gracefully and returns null. | [`SecretRetrieverTests.cs:L34`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SecretRetrieverTests.cs#L34) | Backend xUnit |
| `SEC-05` | Positive | `SEC` | renders RPC message stream with formatted JSON | [`LogsTerminalCard.test.tsx:L61`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LogsTerminalCard.test.tsx#L61) | Frontend Vitest |
| `SEC-ADMIN-AUDIT-REDACTION` | Positive | `SEC` | AdminMcpServer redacts sensitive secrets from argument payloads before recording audit logs. | [`AdminMcpServerTests.cs:L613`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminMcpServerTests.cs#L613) | Backend xUnit |
| `SEC-APPKEY-SANITIZE-METADATA-GET` | Positive | `SEC` | AppKeys API returns sanitized key metadata without leaking plaintext tokens. | [`AppKeysControllerTests.cs:L259`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AppKeysControllerTests.cs#L259) | Backend xUnit |
| `SEC-AUDIT-LOG-ADMIN-ACTION-RECORD` | Positive | `SEC` | AuditLogger records administrative configuration changes and security events to AdminAuditLogs table. | [`AuditLoggerTests.cs:L75`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditLoggerTests.cs#L75) | Backend xUnit |
| `SEC-AUDIT-LOG-INVOCATION-RECORD` | Positive | `SEC` | AuditLogger writes tool invocation audit records with actor attribution and duration to AuditLogs table. | [`AuditLoggerTests.cs:L60`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditLoggerTests.cs#L60) | Backend xUnit |
| `SEC-AUDIT-LOGGER-ADMIN-PERSIST` | Positive | `SEC` | AuditLogger persists administrative actions directly into AdminAuditLogs database table. | [`AuditQueryApiTests.cs:L150`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L150) | Backend xUnit |
| `SEC-AUDIT-MAPPING-SAVE-ACTION` | Positive | `SEC` | PermissionsController records audit action when group mapping is saved. | [`AuditQueryApiTests.cs:L117`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L117) | Backend xUnit |
| `SEC-AUDIT-PER-REQUEST-ACTOR-ATTRIBUTION` | Positive | `SEC` | Audit logger attributes per-request actor credentials accurately across stateless calls. | [`McpIntegrationTests.cs:L128`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L128) | Backend xUnit |
| `SEC-AUDIT-POLICY-SAVE-ACTION` | Positive | `SEC` | PermissionsController records audit action when access policy is saved. | [`AuditQueryApiTests.cs:L83`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L83) | Backend xUnit |
| `SEC-AUDIT-QUERY-FILTERED-ROWS` | Positive | `SEC` | Audit queries support filtering by user, server, and pagination while recording query access in audit log. | [`AuditQueryApiTests.cs:L60`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AuditQueryApiTests.cs#L60) | Backend xUnit |
| `SEC-GATEWAY-ZERO-CONFIG-BOOT` | Positive | `SEC` | Gateway boots from a blank slate with zero master key environment variables, auto-generates .master.key, and serves health and admin endpoints. | [`AdminAutomationSkillTests.cs:L352`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/AdminAutomationSkillTests.cs#L352) | Backend xUnit |
| `SEC-HTTP-SECRET-URL-FALLBACK` | Positive | `SEC` | HttpTransport falls back to URL and SecretItemKey when specific secret paths are unconfigured. | [`TransportsAuthShapeTests.cs:L160`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L160) | Backend xUnit |
| `SEC-KEY-PROVIDER-AUTOGEN` | Positive | `SEC` | EncryptionKeyProvider delegates to DbKeyHelper to auto-generate master key when unconfigured. | [`EncryptionKeyProviderTests.cs:L42`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L42) | Backend xUnit |
| `SEC-KEY-PROVIDER-CONFIG` | Positive | `SEC` | EncryptionKeyProvider returns configured DB_ENCRYPTION_KEY or MCG_SECRET. | [`EncryptionKeyProviderTests.cs:L28`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L28) | Backend xUnit |
| `SEC-KEY-PROVIDER-FALLBACK` | Positive | `SEC` | EncryptionKeyProvider falls back to DB_ENCRYPTION_KEY when MCG_SECRET is unconfigured. | [`EncryptionKeyProviderTests.cs:L70`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L70) | Backend xUnit |
| `SEC-KEY-PROVIDER-SECRET` | Positive | `SEC` | EncryptionKeyProvider returns configured MCG_SECRET. | [`EncryptionKeyProviderTests.cs:L56`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EncryptionKeyProviderTests.cs#L56) | Backend xUnit |
| `SEC-KEYFILE-AUTOGEN` | Positive | `SEC` | Blank-slate initialization auto-generates a 256-bit base64 master key and persists it to .master.key. | [`DbKeyHelperTests.cs:L63`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L63) | Backend xUnit |
| `SEC-KEYFILE-ENV-PRECEDENCE` | Positive | `SEC` | Explicit environment variables MCG_MASTER_KEY or MCG_SECRET take precedence over keyfiles. | [`DbKeyHelperTests.cs:L28`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L28) | Backend xUnit |
| `SEC-KEYFILE-FILE-OVER-KEYFILE` | Positive | `SEC` | Explicit file secrets take precedence over persistent .master.key files. | [`DbKeyHelperTests.cs:L123`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L123) | Backend xUnit |
| `SEC-KEYFILE-FILE-SECRET` | Positive | `SEC` | File-based secrets configured via MCG_MASTER_KEY_FILE or standard Docker secrets paths are resolved. | [`DbKeyHelperTests.cs:L45`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L45) | Backend xUnit |
| `SEC-KEYFILE-HIERARCHY-PRECEDENCE` | Positive | `SEC` | Explicit environment variables take precedence over file secrets and keyfiles. | [`DbKeyHelperTests.cs:L101`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L101) | Backend xUnit |
| `SEC-KEYFILE-RELOAD` | Positive | `SEC` | Existing .master.key file is loaded across gateway restarts without key mutation. | [`DbKeyHelperTests.cs:L83`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L83) | Backend xUnit |
| `SEC-KEYSOURCE-DETECTION` | Positive | `SEC` | Correctly identifies KeySource origin for environment, file, and auto-generated keys. | [`DbKeyHelperTests.cs:L144`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L144) | Backend xUnit |
| `SEC-KEYSOURCE-SETCACHEDKEY` | Positive | `SEC` | SetCachedKey sets in-memory encryption key and updates ActiveKeySource. | [`DbKeyHelperTests.cs:L314`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L314) | Backend xUnit |
| `SEC-LOG-PROVIDER-PRESERVE-PLAIN` | Positive | `SEC` | SanitizingLoggerProvider preserves non-sensitive log statements intact. | [`SanitizingLoggerProviderTests.cs:L43`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L43) | Backend xUnit |
| `SEC-LOG-PROVIDER-REDACT-EXCEPTIONS` | Positive | `SEC` | SanitizingLoggerProvider sanitizes exception messages and stack traces to prevent credential leaks in error logs. | [`SanitizingLoggerProviderTests.cs:L76`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L76) | Backend xUnit |
| `SEC-LOG-PROVIDER-REDACT-SECRETS` | Positive | `SEC` | SanitizingLoggerProvider automatically redacts Bearer tokens, API keys, and credentials in log message strings. | [`SanitizingLoggerProviderTests.cs:L8`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SanitizingLoggerProviderTests.cs#L8) | Backend xUnit |
| `SEC-MASTERKEY-ATOMIC-REENCRYPTION` | Positive | `SEC` | Atomically re-encrypts database credentials when setting a custom master key. | [`MasterKeyReEncryptionTests.cs:L142`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/MasterKeyReEncryptionTests.cs#L142) | Backend xUnit |
| `SEC-MASTERKEY-CONFIGURED-STATUS-BADGE` | Positive | `SEC` | Displays configured badge and rotate button when custom master key is configured. | [`GeneralTab.test.tsx:L192`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L192) | Frontend Vitest |
| `SEC-MASTERKEY-CUSTOM-MODAL-REENCRYPTION` | Positive | `SEC` | Validates master key inputs (length, match) and triggers atomic re-encryption. | [`MasterKeyModal.test.tsx:L6`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MasterKeyModal.test.tsx#L6) | Frontend Vitest |
| `SEC-MASTERKEY-EXTERNAL-LOCKED-BADGE` | Positive | `SEC` | Displays locked badge when master key is externally managed via Vault or Environment. | [`GeneralTab.test.tsx:L149`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L149) | Frontend Vitest |
| `SEC-MASTERKEY-UI-STATUS-BANNER` | Positive | `SEC` | Displays warning banner when keySource is AutoGenerated and opens custom master key modal. | [`GeneralTab.test.tsx:L115`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/GeneralTab.test.tsx#L115) | Frontend Vitest |
| `SEC-PII-BASIC-COOKIE-QUERY-USERINFO` | Positive | `SEC` | PiiSanitizer redacts Basic auth credentials, session cookies, query access tokens, and URL userinfo credentials. | [`PiiSanitizerTests.cs:L53`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L53) | Backend xUnit |
| `SEC-PII-BEARER-TOKEN` | Positive | `SEC` | PiiSanitizer redacts Bearer authentication tokens from payload strings. | [`PiiSanitizerTests.cs:L5`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L5) | Backend xUnit |
| `SEC-PII-CONNECTION-STRING-PASSWORD` | Positive | `SEC` | PiiSanitizer redacts database connection string passwords. | [`PiiSanitizerTests.cs:L29`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L29) | Backend xUnit |
| `SEC-PII-JSON-APIKEY-PASSWORD` | Positive | `SEC` | PiiSanitizer redacts apiKey, password, and secret properties in JSON payloads. | [`PiiSanitizerTests.cs:L16`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L16) | Backend xUnit |
| `SEC-PII-LOGBUFFER-IN-MEMORY` | Positive | `SEC` | LogBuffer sanitizes PII and credentials prior to buffering in memory. | [`PiiSanitizerTests.cs:L40`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PiiSanitizerTests.cs#L40) | Backend xUnit |
| `SEC-PROVIDER-GUARD-CORRUPT-ENCRYPTED-FIELD` | **Guardrail** | `SEC` | Router must not overwrite corrupt encrypted database fields if an update occurs without user reset. | [`ProviderSettingsEncryptionTests.cs:L388`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L388) | Backend xUnit |
| `SEC-PROVIDER-MASK-SECRETS-GET` | Positive | `SEC` | ProvidersController GET endpoints mask sensitive tokens and passwords as asterisks. | [`ProviderSettingsEncryptionTests.cs:L130`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L130) | Backend xUnit |
| `SEC-PROVIDER-REDACT-AUDIT-PAYLOADS` | Positive | `SEC` | ProvidersController redacts sensitive secrets in administrative audit logs. | [`ProviderSettingsEncryptionTests.cs:L185`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ProviderSettingsEncryptionTests.cs#L185) | Backend xUnit |
| `SEC-SESSIONID-OPAQUE-NOT-BEARER` | Positive | `SEC` | Mcp-Session-Id header generates opaque UUIDs without leaking bearer tokens. | [`McpIntegrationTests.cs:L1119`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/McpIntegrationTests.cs#L1119) | Backend xUnit |
| `SEC-VAULT-BOOTSTRAPPING` | Positive | `SEC` | Bootstraps master encryption key directly from HashiCorp Vault when VAULT_ADDR is configured. | [`DbKeyHelperTests.cs:L191`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L191) | Backend xUnit |
| `SEC-VAULT-CUSTOM-MOUNT-PATH` | Positive | `SEC` | SseTransport resolves dynamic secrets from Vault using custom mounts, paths, and secret fields. | [`TransportsAuthShapeTests.cs:L134`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L134) | Backend xUnit |
| `SEC-VAULT-CUSTOM-PATH` | Positive | `SEC` | Bootstraps master key from Vault using custom mount path and secret key name. | [`DbKeyHelperTests.cs:L236`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/DbKeyHelperTests.cs#L236) | Backend xUnit |
| `UI-105` | Positive | `SEC` | renders system logs and handles level filter | [`LogsTerminalCard.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/LogsTerminalCard.test.tsx#L1) | Frontend Vitest |
| `TRANS-01` | Positive | `TRANS` | HttpTransport formats X-API-Key header when downstream server AuthShape is 'x-api-key'. | [`EnterpriseAuthAndVaultScenarioTests.cs:L258`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/EnterpriseAuthAndVaultScenarioTests.cs#L258) | Backend xUnit |
| `TRANS-02` | Positive | `TRANS` | Register STDIO server with Env provider, verify connection card, and execute tool via Test Bench. | [`full-ui-flow-stdio-env.spec.ts:L8`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/full-ui-flow-stdio-env.spec.ts#L8) | Playwright E2E |
| `TRANS-04` | Positive | `TRANS` | HTTP stateless transport correctly accumulates multi-line SSE streams and skips intermediate notification events | [`HttpTransportTests.cs:L72`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L72) | Backend xUnit |
| `TRANS-05` | Positive | `TRANS` | HTTP stateless transport reads entire multi-line and formatted JSON response bodies without premature truncation | [`HttpTransportTests.cs:L109`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L109) | Backend xUnit |
| `TRANS-06` | Positive | `TRANS` | HTTP stateless transport joins multi-line SSE data fields into complete JSON payloads | [`HttpTransportTests.cs:L146`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L146) | Backend xUnit |
| `TRANS-AUTH-SHAPES-CUSTOM-HEADER` | Positive | `TRANS` | SseTransport applies custom header names for proprietary target backend authentication. | [`TransportsAuthShapeTests.cs:L45`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L45) | Backend xUnit |
| `TRANS-AUTH-SHAPES-HEADERS-JSON` | Positive | `TRANS` | SseTransport parses and applies extra request headers from HeadersJson configuration. | [`TransportsAuthShapeTests.cs:L112`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L112) | Backend xUnit |
| `TRANS-AUTH-SHAPES-QUERY-PARAM` | Positive | `TRANS` | SseTransport appends authentication tokens as URL query parameters when query auth shape is configured. | [`TransportsAuthShapeTests.cs:L67`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L67) | Backend xUnit |
| `TRANS-AUTH-SHAPES-STANDARD-HEADERS` | Positive | `TRANS` | SseTransport formats standard authorization shapes (bearer, basic, raw, x-api-key) into HTTP headers. | [`TransportsAuthShapeTests.cs:L17`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L17) | Backend xUnit |
| `TRANS-BACKEND-MULTIPLEX-CONCURRENT-RPC` | Positive | `TRANS` | BackendConnection multiplexes 100+ concurrent asynchronous polymorphic RPC requests without deadlocking. | [`ChallengerTests.cs:L494`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L494) | Backend xUnit |
| `TRANS-DISCONNECT-PENDING-CLEANUP` | Positive | `TRANS` | Cleans up and cancels pending requests upon backend transport disconnect. | [`ConcurrentResponseIsolationTests.cs:L277`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L277) | Backend xUnit |
| `TRANS-EXPLICIT-NULL-ID-ISOLATION` | Positive | `TRANS` | Handles JSON-RPC requests with explicit null IDs and multiplexes upstream calls correctly. | [`ConcurrentResponseIsolationTests.cs:L346`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L346) | Backend xUnit |
| `TRANS-HIGH-CONCURRENCY-ISOLATION` | Positive | `TRANS` | Maintains strict response isolation under high concurrency with 100+ callers reusing identical RPC IDs. | [`ConcurrentResponseIsolationTests.cs:L111`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L111) | Backend xUnit |
| `TRANS-HTTP-AUTH-CUSTOM-HEADER` | Positive | `TRANS` | HttpTransport formats custom header authentication for target servers. | [`TransportsAuthShapeTests.cs:L90`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportsAuthShapeTests.cs#L90) | Backend xUnit |
| `TRANS-HTTP-DISPOSED-GUARD` | **Guardrail** | `TRANS` | HttpTransport SendRequestAsync returns -32001 Not Connected when transport has been disposed. | [`TransportResilienceTests.cs:L59`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L59) | Backend xUnit |
| `TRANS-HTTP-RESOLVE-STATIC-APIKEY` | Positive | `TRANS` | HTTP stateless transport resolves static API keys when secret provider is None | [`HttpTransportTests.cs:L11`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/HttpTransportTests.cs#L11) | Backend xUnit |
| `TRANS-ISOLATION-SAME-ID-REVERSED-ORDER` | Positive | `TRANS` | Multiplexes concurrent client calls sharing identical JSON-RPC IDs and routes reversed responses correctly. | [`ConcurrentResponseIsolationTests.cs:L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L12) | Backend xUnit |
| `TRANS-MIXED-ID-TYPES-ISOLATION` | Positive | `TRANS` | Handles mixed numeric, string, and null JSON-RPC IDs concurrently across backend transports. | [`ConcurrentResponseIsolationTests.cs:L614`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L614) | Backend xUnit |
| `TRANS-NOTIFICATION-NO-RESPONSE-LISTENER` | Positive | `TRANS` | Handles JSON-RPC notifications without registering pending response listeners. | [`ConcurrentResponseIsolationTests.cs:L419`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L419) | Backend xUnit |
| `TRANS-SSE-CALLMETHOD-DISCONNECT-GUARD` | **Guardrail** | `TRANS` | SseTransport CallMethodAsync returns -32001 Not Connected when backend is disconnected. | [`TransportResilienceTests.cs:L12`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L12) | Backend xUnit |
| `TRANS-SSE-LOG-ENDPOINT-WAIT-EXCEPTION` | Positive | `TRANS` | SSE transport logs exceptions gracefully when waiting for endpoint URL without throwing unhandled exceptions. | [`SseTransportTests.cs:L76`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L76) | Backend xUnit |
| `TRANS-SSE-NOTIF-FORWARD-FIELDS-INTACT` | Positive | `TRANS` | SSE backend notifications are forwarded to client sessions with all payload fields intact. | [`ChallengerTests.cs:L419`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L419) | Backend xUnit |
| `TRANS-SSE-RESOLVE-STATIC-APIKEY` | Positive | `TRANS` | SSE transport resolves static plaintext API keys when provider is None | [`SseTransportTests.cs:L13`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L13) | Backend xUnit |
| `TRANS-SSE-SENDREQUEST-DISCONNECT-GUARD` | **Guardrail** | `TRANS` | SseTransport SendRequestAsync returns -32001 Not Connected when backend is disconnected. | [`TransportResilienceTests.cs:L36`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L36) | Backend xUnit |
| `TRANS-SSE-STREAM-LIFECYCLE` | Positive | `TRANS` | SSE transport correctly resolves relative endpoint URLs and ignores keep-alive SSE comments. | [`SseTransportTests.cs:L100`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/SseTransportTests.cs#L100) | Backend xUnit |
| `TRANS-STATELESS-CANCELLATION-ISOLATION` | Positive | `TRANS` | Isolates cancellation tokens between concurrent stateless client requests. | [`ConcurrentResponseIsolationTests.cs:L482`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L482) | Backend xUnit |
| `TRANS-STDIO-DISPOSED-GUARD` | **Guardrail** | `TRANS` | StdioTransport SendRequestAsync returns -32001 Process Not Running when transport has been disposed. | [`TransportResilienceTests.cs:L101`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/TransportResilienceTests.cs#L101) | Backend xUnit |
| `TRANS-STDIO-DRAIN-BUFFER-EOF` | Positive | `TRANS` | STDIO transport drains buffered stdout/stderr streams to EOF when process exits rapidly | [`StdioTransportTests.cs:L472`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L472) | Backend xUnit |
| `TRANS-STDIO-SPAWN-TOOL-CALL` | Positive | `TRANS` | STDIO transport spawns subprocess, handles JSON-RPC initialization and executes tool calls | [`StdioTransportTests.cs:L49`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L49) | Backend xUnit |
| `TRANS-STDIO-STREAM-STDERR-LOGS` | Positive | `TRANS` | STDIO transport streams subprocess stderr asynchronously to structured router diagnostic logs | [`StdioTransportTests.cs:L170`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L170) | Backend xUnit |
| `TRANS-STDIO-TERMINATE-CLEANLY` | Positive | `TRANS` | STDIO transport terminates subprocess tree cleanly upon disposal or cancellation | [`StdioTransportTests.cs:L242`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L242) | Backend xUnit |
| `TRANS-STDIO-TOKENIZE-PRESERVE-QUOTES` | Positive | `TRANS` | STDIO command-line tokenizer preserves quoted arguments and space escaping | [`StdioTransportTests.cs:L327`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/StdioTransportTests.cs#L327) | Backend xUnit |
| `TRANS-TARGETED-CANCELLATION-ISOLATION` | Positive | `TRANS` | Targeted cancellation does not cancel concurrent client sessions reusing identical RPC IDs. | [`ConcurrentResponseIsolationTests.cs:L532`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ConcurrentResponseIsolationTests.cs#L532) | Backend xUnit |
| `TRANS-TIMEOUT-PENDING-CLEANUP` | Positive | `TRANS` | SendRequestAsync times out cleanly and removes pending completion handlers without leaking memory. | [`ChallengerTests.cs:L276`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ChallengerTests.cs#L276) | Backend xUnit |
| `TRANS-VALIDATION-HTTP-ALLOWED-IPS` | Positive | `TRANS` | ServerValidationHelper accepts valid HTTP/HTTPS endpoints allowed by IP security rules. | [`ServerEndpointsValidationTests.cs:L38`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/ServerEndpointsValidationTests.cs#L38) | Backend xUnit |
| `UI-01` | Positive | `UI` | opens confirmation modal and resolves true when confirmed | [`useConfirmStore.test.ts:L31`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useConfirmStore.test.ts#L31) | Frontend Vitest |
| `UI-02` | Positive | `UI` | Inspect modal displays spinner loading state while querying server capabilities | [`ServerInspectModal.test.tsx:L61`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L61) | Frontend Vitest |
| `UI-03` | Positive | `UI` | Grouped server view renders category sections and supports collapsible groups | [`DashboardView.test.tsx:L63`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/DashboardView.test.tsx#L63) | Frontend Vitest |
| `UI-04` | Positive | `UI` | Tool selector filters available tools by selected backend server | [`ToolTesterCard.test.tsx:L77`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L77) | Frontend Vitest |
| `UI-05` | Positive | `UI` | Router allows customized branding parameters (DashboardTitle, DashboardIcon) to be saved and retrieved via the API. | [`PipelineIntegrationTests.cs:L253`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L253) | Backend xUnit |
| `UI-06` | Positive | `UI` | Router supports uploading and retrieving custom branding logo images via dedicated endpoints. | [`PipelineIntegrationTests.cs:L447`](https://github.com/spelech/model-context-gateway/blob/main/ModelContextGateway.Tests/PipelineIntegrationTests.cs#L447) | Backend xUnit |
| `UI-07` | Positive | `UI` | Audits desktop viewport layout for zero horizontal overflow and high UX score. | [`layout-inspector.spec.ts:L38`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/layout-inspector.spec.ts#L38) | Playwright E2E |
| `UI-102` | Positive | `UI` | Dashboard renders stats card, connected server list, and setup instructions | [`DashboardView.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/DashboardView.test.tsx#L1) | Frontend Vitest |
| `UI-103` | Positive | `UI` | Interactive tool tester renders server and tool selection dropdowns | [`ToolTesterCard.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ToolTesterCard.test.tsx#L1) | Frontend Vitest |
| `UI-108` | Positive | `UI` | renders nothing when isMappingModalOpen is false | [`MappingModal.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/MappingModal.test.tsx#L1) | Frontend Vitest |
| `UI-109` | Positive | `UI` | Renders ClientSetupGuide below the user credentials card. | [`MyMcpServers.test.tsx:L102`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/pages/MyMcpServers.test.tsx#L102) | Frontend Vitest |
| `UI-110` | Positive | `UI` | renders title, MCG badge, subtitle, and version badge | [`Header.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/Header.test.tsx#L1) | Frontend Vitest |
| `UI-111` | Positive | `UI` | renders GeneralTab and triggers save | [`SettingsTabs.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsTabs.test.tsx#L1) | Frontend Vitest |
| `UI-113` | Positive | `UI` | renders tab navigation and switches active subviews | [`SettingsView.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SettingsView.test.tsx#L1) | Frontend Vitest |
| `UI-115` | Positive | `UI` | renders test bench cards and switches tabs | [`TestBenchView.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/TestBenchView.test.tsx#L1) | Frontend Vitest |
| `UI-116` | Positive | `UI` | Modal remains hidden when isInspectOpen is false | [`ServerInspectModal.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ServerInspectModal.test.tsx#L1) | Frontend Vitest |
| `UI-117` | Positive | `UI` | returns null when isOpen is false | [`SharedComponents.test.tsx:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/SharedComponents.test.tsx#L1) | Frontend Vitest |
| `UI-119` | Positive | `UI` | calls server endpoints correctly | [`typedApi.test.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/api/typedApi.test.ts#L1) | Frontend Vitest |
| `UI-122` | Positive | `UI` | should navigate to Settings view and configure vector embedding options | [`settings.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/settings.spec.ts#L1) | Playwright E2E |
| `UI-124` | Positive | `UI` | Renders main dashboard navigation tabs and layout headers | [`dashboard.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/dashboard.spec.ts#L1) | Playwright E2E |
| `UI-128` | Positive | `UI` | should navigate to Test Bench view and render tester cards | [`testbench.spec.ts:L1`](https://github.com/spelech/model-context-gateway/blob/main/frontend/e2e/testbench.spec.ts#L1) | Playwright E2E |
| `UI-30` | Positive | `UI` | Renders client registration form with inputs for name, client type, redirect URIs, grant types, scopes, and expiration. | [`ClientModal.test.tsx:L27`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/components/ClientModal.test.tsx#L27) | Frontend Vitest |
| `UI-31` | **Guardrail** | `UI` | Fetches registered OAuth clients and updates store state. | [`useClientStore.test.ts:L37`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L37) | Frontend Vitest |
| `UI-32` | Positive | `UI` | Registers OAuth client with extended metadata (redirect URIs, grant types, client type, expiration) and captures one-time credentials. | [`useClientStore.test.ts:L76`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useClientStore.test.ts#L76) | Frontend Vitest |
| `UI-CONFIRM-MODAL` | **Guardrail** | `UI` | Centralized promise-based confirmation store resolves true on confirmation and false on cancellation. | [`useConfirmStore.test.ts:L4`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/stores/useConfirmStore.test.ts#L4) | Frontend Vitest |
| `UI-TOAST-TRANSITION` | **Guardrail** | `UI` | Displays error toast notification when saving invalid JSON credentials for user-provided server. | [`MyMcpServers.test.tsx:L23`](https://github.com/spelech/model-context-gateway/blob/main/frontend/src/test/pages/MyMcpServers.test.tsx#L23) | Frontend Vitest |
