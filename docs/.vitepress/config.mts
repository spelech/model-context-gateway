import { defineConfig } from 'vitepress';
import { withMermaid } from 'vitepress-plugin-mermaid';

export default withMermaid(
  defineConfig({
    title: 'Model Context Gateway (MCG)',
    description:
      'Enterprise C# ASP.NET Core Gateway Router, OAuth 2.0 Provider, and Semantic Proxy for the Model Context Protocol (MCP).',
    base: '/model-context-gateway/',
    lang: 'en-US',
    cleanUrls: true,
    lastUpdated: true,
    appearance: 'dark',
    ignoreDeadLinks: 'localhostLinks',
    srcExclude: ['superpowers/**'],

    head: [
      ['link', { rel: 'icon', href: '/model-context-gateway/favicon.ico' }],
      ['meta', { name: 'theme-color', content: '#00c853' }],
      ['link', { rel: 'preconnect', href: 'https://fonts.googleapis.com' }],
      ['link', { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' }],
      [
        'link',
        {
          rel: 'stylesheet',
          href: 'https://fonts.googleapis.com/css2?family=JetBrains+Mono:ital,wght@0,400;0,500;0,600;0,700;1,400&family=Outfit:wght@300;400;500;600;700;800&display=swap'
        }
      ]
    ],

    themeConfig: {
      siteTitle: 'Model Context Gateway',

      nav: [
        { text: 'Home', link: '/' },
        { text: 'Getting Started', link: '/features-guide' },
        { text: 'Deployments', link: '/deployment/' },
        { text: 'User Guide', link: '/user-guide/' },
        {
          text: 'Architecture & Security',
          items: [
            { text: 'Architecture Overview', link: '/architecture/' },
            { text: 'Component & Boundary Model', link: '/architecture/components' },
            { text: 'Routing & Meta-Mode', link: '/architecture/routing-and-meta-mode' },
            { text: 'Authorization Pipeline', link: '/architecture/authorization-pipeline' },
            { text: 'Transports & Subprocesses', link: '/architecture/transports-and-subprocesses' },
            { text: 'Database & Envelope Encryption', link: '/architecture/database-and-encryption' },
            { text: 'Authentication Architecture', link: '/authentication-architecture' },
            { text: 'Active Directory & Multi-Level RBAC', link: '/active-directory-and-rbac-guide' },
            { text: 'OIDC & SSO Reverse Proxy', link: '/oidc-and-sso-guide' },
            { text: 'Downstream Auth & Delegation', link: '/downstream-auth-and-delegation-guide' },
            { text: 'Transports (Detailed Guide)', link: '/transports' },
            { text: 'Data Model & ERD', link: '/data-model' },
            { text: 'Database Providers', link: '/database-providers' },
            { text: 'Secret Providers & Key Management', link: '/secret-providers' },
            { text: 'AppKey Scopes & RBAC', link: '/appkey-scopes' }
          ]
        },
        {
          text: 'Operations',
          items: [
            { text: 'Administrator Guide', link: '/admin-guide' },
            { text: 'Admin MCP Automation', link: '/admin-mcp-automation-guide' },
            { text: 'Admin MCP Tools Reference', link: '/admin-mcp-features' },
            { text: 'Operations Runbook', link: '/runbook' },
            { text: 'Troubleshooting & RCA', link: '/mcp-routing-and-admin-issues' }
          ]
        },
        {
          text: 'Development & Quality',
          items: [
            { text: 'Developer Guide', link: '/developer-guide' },
            { text: 'CI/CD Quality Gates', link: '/ci-quality-gates' },
            { text: 'Test Catalog Taxonomy', link: '/test-catalog-guide' },
            { text: 'Software Requirements (SRS)', link: '/software-requirements-and-test-catalog' },
            { text: 'Testing Matrix', link: '/testing-matrix' },
            { text: 'Test Coverage Evaluation', link: '/test-coverage-evaluation' },
            { text: 'Code Coverage Report', link: '/coverage-report' },
            { text: 'Evaluation Guide', link: '/evaluation-guide' }
          ]
        }
      ],

      sidebar: [
        {
          text: 'Getting Started',
          collapsed: false,
          items: [
            { text: 'Features Overview', link: '/features-guide' },
            { text: 'Deployment Overview', link: '/deployment/' },
            { text: 'Single-User & Home-Lab Setup', link: '/deployment/homelab' },
            { text: 'Docker & Container Deployment', link: '/deployment/docker' },
            { text: 'Windows & IIS Deployment', link: '/deployment/windows-iis' },
            { text: 'Support Matrix', link: '/support-matrix' }
          ]
        },
        {
          text: 'Deployment & Hosting',
          collapsed: false,
          items: [
            { text: 'Overview & Topologies', link: '/deployment/' },
            { text: 'Docker & Containers', link: '/deployment/docker' },
            { text: 'Windows IIS In-Process', link: '/deployment/windows-iis' },
            { text: 'Windows Service (SCM) & DPAPI', link: '/deployment/windows-service' },
            { text: 'Multi-Provider Database Setup', link: '/deployment/database-setup' },
            { text: 'Single-User & Home-Lab Setup', link: '/deployment/homelab' },
            { text: 'Validation Runbook & Troubleshooting', link: '/deployment/validation-and-runbook' }
          ]
        },
        {
          text: 'User Guide',
          collapsed: false,
          items: [
            { text: 'User Guide Overview', link: '/user-guide/' },
            { text: 'Dashboard & Navigation', link: '/user-guide/dashboard' },
            { text: 'Server Management & Secrets', link: '/user-guide/servers' },
            { text: 'RBAC, Security & Policies', link: '/user-guide/rbac-and-policies' },
            { text: 'AppKey Management & Scopes', link: '/user-guide/app-keys' },
            {
              text: 'Client Setup & Integration',
              collapsed: true,
              items: [
                { text: 'Client Setup Overview', link: '/user-guide/clients/' },
                { text: 'Cursor IDE', link: '/user-guide/clients/cursor' },
                { text: 'Claude Desktop', link: '/user-guide/clients/claude-desktop' },
                { text: 'Cline & VS Code', link: '/user-guide/clients/cline-and-vscode' },
                { text: 'Antigravity CLI & Agents', link: '/user-guide/clients/antigravity' }
              ]
            },
            {
              text: 'Interactive Test Bench',
              collapsed: true,
              items: [
                { text: 'Test Bench Overview', link: '/user-guide/test-bench/' },
                { text: 'Tool Execution Tester', link: '/user-guide/test-bench/tool-tester' },
                { text: 'Virtual Resources & Prompts', link: '/user-guide/test-bench/resources-and-prompts' },
                { text: 'Semantic Router Simulator', link: '/user-guide/test-bench/semantic-search' },
                { text: 'Raw Console & Live Logs', link: '/user-guide/test-bench/console-and-logs' }
              ]
            },
            { text: 'System Settings & Embeddings', link: '/user-guide/settings' }
          ]
        },
        {
          text: 'Administration & Operations',
          collapsed: true,
          items: [
            { text: 'Administrator Guide', link: '/admin-guide' },
            { text: 'Admin MCP Automation Guide', link: '/admin-mcp-automation-guide' },
            { text: 'Admin MCP Tools Reference', link: '/admin-mcp-features' },
            { text: 'Operations Runbook', link: '/runbook' },
            { text: 'Troubleshooting & RCA', link: '/mcp-routing-and-admin-issues' }
          ]
        },
        {
          text: 'Security & Authentication',
          collapsed: true,
          items: [
            { text: 'Authentication Architecture', link: '/authentication-architecture' },
            { text: 'Active Directory & Multi-Level RBAC', link: '/active-directory-and-rbac-guide' },
            { text: 'OIDC & SSO Reverse Proxy', link: '/oidc-and-sso-guide' },
            { text: 'Downstream Auth & Delegation', link: '/downstream-auth-and-delegation-guide' },
            { text: 'MCP Server Auth Cookbook', link: '/mcp-server-auth-cookbook' },
            { text: 'AppKey Scopes & RBAC', link: '/appkey-scopes' },
            { text: 'Secret Providers & Key Management', link: '/secret-providers' },
            {
              text: 'Authentication Flows',
              collapsed: true,
              items: [
                { text: 'Auth Support Matrix', link: '/auth-flows/auth-support-matrix' },
                { text: 'MCP Request Auth Flow', link: '/auth-flows/mcp-request-auth-flow' },
                { text: 'Platform Config Flow', link: '/auth-flows/platform-config-flow' },
                { text: 'MCP Server Config Flow', link: '/auth-flows/mcp-server-config-flow' },
                { text: 'Multi-Tenant OAuth Consent', link: '/auth-flows/multi-tenant-oauth-consent' },
                { text: 'Dynamic Client Registration (RFC 7591)', link: '/auth-flows/dynamic-client-registration' },
                { text: 'Dynamic Auth Limitations', link: '/auth-flows/dynamic-auth-limitations' }
              ]
            }
          ]
        },
        {
          text: 'Architecture & Internals',
          collapsed: true,
          items: [
            { text: 'Architecture Overview', link: '/architecture/' },
            { text: 'Component & Boundary Model', link: '/architecture/components' },
            { text: 'Routing & Meta-Mode Engine', link: '/architecture/routing-and-meta-mode' },
            { text: 'Authorization Pipeline', link: '/architecture/authorization-pipeline' },
            { text: 'Transports & Subprocesses', link: '/architecture/transports-and-subprocesses' },
            { text: 'Database & Envelope Encryption', link: '/architecture/database-and-encryption' },
            { text: 'Transports (Detailed Guide)', link: '/transports' },
            { text: 'Data Model & ERD', link: '/data-model' },
            { text: 'Database Providers Matrix', link: '/database-providers' },
            { text: 'ID Mapping Blueprint', link: '/id-mapping-blueprint' }
          ]
        },
        {
          text: 'Development & Quality',
          collapsed: true,
          items: [
            { text: 'Developer & Contributor Guide', link: '/developer-guide' },
            { text: 'CI/CD Quality Gates', link: '/ci-quality-gates' },
            { text: 'Test Catalog Taxonomy', link: '/test-catalog-guide' },
            { text: 'Software Requirements (SRS) & Test Catalog', link: '/software-requirements-and-test-catalog' },
            { text: 'Testing Matrix', link: '/testing-matrix' },
            { text: 'Test Coverage Evaluation', link: '/test-coverage-evaluation' },
            { text: 'Code Coverage Report', link: '/coverage-report' },
            { text: 'Evaluation Guide', link: '/evaluation-guide' }
          ]
        }
      ],

      socialLinks: [
        { icon: 'github', link: 'https://github.com/spelech/model-context-gateway' }
      ],

      search: {
        provider: 'local'
      },

      editLink: {
        pattern: 'https://github.com/spelech/model-context-gateway/edit/main/docs/:path',
        text: 'Edit this page on GitHub'
      },

      footer: {
        message: 'Released under the Apache 2.0 License.',
        copyright: 'Copyright © 2026 Steve Pelech. Model Context Gateway (MCG).'
      }
    },

    mermaid: {
      theme: 'dark'
    }
  })
);
