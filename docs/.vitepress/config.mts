import { defineConfig } from 'vitepress';
import { withMermaid } from 'vitepress-plugin-mermaid';

const gettingStartedSidebar = [
  {
    text: 'Getting Started',
    items: [
      { text: 'Overview & Quickstart', link: '/getting-started/' },
      { text: 'Features Overview', link: '/features-guide' },
      { text: 'Support Matrix', link: '/support-matrix' }
    ]
  },
  {
    text: 'Deployment Options',
    items: [
      { text: 'Deployment Overview', link: '/deployment/' },
      { text: 'Docker & Containers', link: '/deployment/docker' },
      { text: 'Windows & IIS Deployment', link: '/deployment/windows-iis' },
      { text: 'Windows Service (SCM)', link: '/deployment/windows-service' },
      { text: 'Homelab Setup', link: '/deployment/homelab' },
      { text: 'Database Setup', link: '/deployment/database-setup' },
      { text: 'Validation & Runbook', link: '/deployment/validation-and-runbook' }
    ]
  }
];

const userGuideSidebar = [
  {
    text: 'User Guide',
    items: [
      { text: 'User Guide Overview', link: '/user-guide/' },
      { text: 'Dashboard & Navigation', link: '/user-guide/dashboard' },
      { text: 'Server Management & Secrets', link: '/user-guide/servers' },
      { text: 'RBAC, Security & Policies', link: '/user-guide/rbac-and-policies' },
      { text: 'AppKey Management & Scopes', link: '/user-guide/app-keys' },
      { text: 'System Settings & Embeddings', link: '/user-guide/settings' }
    ]
  },
  {
    text: 'Client Setup & Integration',
    collapsed: false,
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
    collapsed: false,
    items: [
      { text: 'Test Bench Overview', link: '/user-guide/test-bench/' },
      { text: 'Tool Execution Tester', link: '/user-guide/test-bench/tool-tester' },
      { text: 'Virtual Resources & Prompts', link: '/user-guide/test-bench/resources-and-prompts' },
      { text: 'Semantic Router Simulator', link: '/user-guide/test-bench/semantic-search' },
      { text: 'Raw Console & Live Logs', link: '/user-guide/test-bench/console-and-logs' }
    ]
  }
];

const architectureSidebar = [
  {
    text: 'Architecture Core',
    items: [
      { text: 'Architecture Overview', link: '/architecture/' },
      { text: 'Components & Boundary Model', link: '/architecture/components' },
      { text: 'Routing & Meta-Mode', link: '/architecture/routing-and-meta-mode' },
      { text: 'Authorization Pipeline', link: '/architecture/authorization-pipeline' },
      { text: 'Transports & Subprocesses', link: '/architecture/transports-and-subprocesses' },
      { text: 'Transports (Detailed Guide)', link: '/transports' },
      { text: 'Database & Encryption', link: '/architecture/database-and-encryption' },
      { text: 'Data Model & ERD', link: '/data-model' }
    ]
  },
  {
    text: 'Security & Authentication',
    collapsed: false,
    items: [
      { text: 'Authentication Architecture', link: '/authentication-architecture' },
      { text: 'Active Directory & Multi-Level RBAC', link: '/active-directory-and-rbac-guide' },
      { text: 'Active Directory & LDAPS Domain Setup', link: '/architecture/security/active-directory-ldap' },
      { text: 'Multi-Level RBAC & Access Policies', link: '/architecture/security/rbac-and-policies' },
      { text: 'Windows Integrated Auth & IIS', link: '/architecture/security/windows-integrated-auth' },
      { text: 'OIDC & SSO Reverse Proxy', link: '/oidc-and-sso-guide' },
      { text: 'Downstream Auth & Delegation', link: '/downstream-auth-and-delegation-guide' },
      { text: 'MCP Server Auth Cookbook', link: '/mcp-server-auth-cookbook' },
      { text: 'Auth Support Matrix', link: '/auth-flows/auth-support-matrix' },
      { text: 'Per-User OAuth Flow', link: '/auth-flows/per-user-oauth-flow' },
      { text: 'Slack Direct MCP Integration', link: '/auth-flows/slack-direct-mcp-integration' }
    ]
  }
];

const operationsSidebar = [
  {
    text: 'Operations & Administration',
    items: [
      { text: 'Operations Overview', link: '/operations/' },
      { text: 'Administrator Guide', link: '/admin-guide' },
      { text: 'Admin MCP Automation Guide', link: '/admin-mcp-automation-guide' },
      { text: 'Admin MCP Tools Reference', link: '/admin-mcp-features' },
      { text: 'Operations Runbook', link: '/runbook' },
      { text: 'Troubleshooting & RCA', link: '/mcp-routing-and-admin-issues' },
      { text: 'Subprocess & STDIO Runbook', link: '/operations/troubleshooting/subprocess-and-stdio' },
      { text: 'Auth & Token Failures Runbook', link: '/operations/troubleshooting/auth-and-token-failures' },
      { text: 'Database Locks & Migrations Runbook', link: '/operations/troubleshooting/database-locks-and-migrations' }
    ]
  }
];

const referenceSidebar = [
  {
    text: 'Technical Reference',
    items: [
      { text: 'Reference Overview', link: '/reference/' },
      { text: 'Software Requirements (SRS)', link: '/software-requirements-and-test-catalog' },
      { text: 'Test Catalog Taxonomy', link: '/test-catalog-guide' },
      { text: 'Database Providers', link: '/database-providers' },
      { text: 'Secret Providers & Keys', link: '/secret-providers' },
      { text: 'AppKey Scopes & RBAC', link: '/appkey-scopes' },
      { text: 'Support Matrix', link: '/support-matrix' }
    ]
  },
  {
    text: 'Development & Quality',
    collapsed: false,
    items: [
      { text: 'Developer & Contributor Guide', link: '/developer-guide' },
      { text: 'CI/CD Quality Gates', link: '/ci-quality-gates' },
      { text: 'Testing Matrix', link: '/testing-matrix' },
      { text: 'Test Coverage Evaluation', link: '/test-coverage-evaluation' },
      { text: 'Code Coverage Report', link: '/coverage-report' },
      { text: 'Evaluation Guide', link: '/evaluation-guide' }
    ]
  }
];

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
      ['link', { rel: 'icon', type: 'image/svg+xml', href: '/model-context-gateway/favicon.svg' }],
      ['link', { rel: 'alternate icon', href: '/model-context-gateway/favicon.ico' }],
      ['meta', { name: 'theme-color', content: '#f97316' }],
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
      logo: '/logo.svg',

      nav: [
        { text: 'Home', link: '/' },
        { text: 'Getting Started', link: '/getting-started/' },
        { text: 'User Guide', link: '/user-guide/' },
        { text: 'Architecture', link: '/architecture/' },
        { text: 'Operations', link: '/operations/' },
        { text: 'Reference', link: '/reference/' }
      ],

      sidebar: {
        '/getting-started/': gettingStartedSidebar,
        '/features-guide': gettingStartedSidebar,
        '/deployment/': gettingStartedSidebar,

        '/user-guide/': userGuideSidebar,

        '/architecture/': architectureSidebar,
        '/authentication-architecture': architectureSidebar,
        '/active-directory-and-rbac-guide': architectureSidebar,
        '/oidc-and-sso-guide': architectureSidebar,
        '/downstream-auth-and-delegation-guide': architectureSidebar,
        '/mcp-server-auth-cookbook': architectureSidebar,
        '/transports': architectureSidebar,
        '/data-model': architectureSidebar,
        '/auth-flows/': architectureSidebar,

        '/operations/': operationsSidebar,
        '/admin-guide': operationsSidebar,
        '/admin-mcp-automation-guide': operationsSidebar,
        '/admin-mcp-features': operationsSidebar,
        '/runbook': operationsSidebar,
        '/mcp-routing-and-admin-issues': operationsSidebar,

        '/reference/': referenceSidebar,
        '/software-requirements-and-test-catalog': referenceSidebar,
        '/test-catalog-guide': referenceSidebar,
        '/database-providers': referenceSidebar,
        '/secret-providers': referenceSidebar,
        '/appkey-scopes': referenceSidebar,
        '/support-matrix': referenceSidebar,
        '/developer-guide': referenceSidebar,
        '/ci-quality-gates': referenceSidebar,
        '/testing-matrix': referenceSidebar,
        '/test-coverage-evaluation': referenceSidebar,
        '/coverage-report': referenceSidebar,
        '/evaluation-guide': referenceSidebar
      },

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

    markdown: {
      languageAlias: {
        caddy: 'nginx',
        caddyfile: 'nginx',
        env: 'bash',
        ldap: 'ini'
      }
    },

    mermaid: {
      theme: 'dark',
      mermaid: {
        fontFamily: 'JetBrains Mono, monospace'
      }
    }
  })
);
