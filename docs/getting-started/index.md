# 🚀 Getting Started & Deployment Overview

Welcome to the **Getting Started & Deployment** guide for Model Context Gateway (MCG).

Model Context Gateway connects Large Language Models, AI coding assistants, and autonomous agents to downstream tools, microservices, and databases through a unified, secure MCP endpoint.

---

<div class="landing-grid">

<a href="../features-guide" class="landing-card">
  <h3>✨ Features Overview</h3>
  <p>Explore core capabilities: Meta-Mode context optimization, semantic vector routing, 4-stage RBAC, and AES-256 envelope encryption.</p>
</a>

<a href="../deployment/docker" class="landing-card">
  <h3>🐳 Docker & Containers</h3>
  <p>Deploy MCG using multi-stage Docker containers, Docker Compose, and Kubernetes with automatic container discovery.</p>
</a>

<a href="../deployment/windows-iis" class="landing-card">
  <h3>🪟 Windows IIS Deployment</h3>
  <p>Production IIS in-process hosting using AspNetCoreModuleV2 with native Kerberos/NTLM Windows Authentication.</p>
</a>

<a href="../deployment/windows-service" class="landing-card">
  <h3>⚙️ Windows Service & DPAPI</h3>
  <p>Run MCG as a dedicated Windows Service (SCM) with registry-backed DPAPI envelope encryption for machine secrets.</p>
</a>

<a href="../deployment/homelab" class="landing-card">
  <h3>🏠 Home-Lab Setup</h3>
  <p>Fast zero-config setup for homelabs, standalone developer workstations, and local LLMs (Ollama, LM Studio).</p>
</a>

<a href="../support-matrix" class="landing-card">
  <h3>📊 Support Matrix</h3>
  <p>Review supported OS platforms, database providers, AI client tools, and downstream MCP transports.</p>
</a>

</div>

<style>
.landing-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 1.25rem;
  margin-top: 1.5rem;
}

.landing-card {
  display: block;
  padding: 1.25rem;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(8px);
  text-decoration: none !important;
  transition: all 0.25s ease;
}

.landing-card:hover {
  transform: translateY(-3px);
  border-color: var(--vp-c-brand);
  background: rgba(255, 255, 255, 0.06);
}

.landing-card h3 {
  margin: 0 0 0.5rem 0 !important;
  font-size: 1.1rem;
  color: var(--vp-c-text-1);
}

.landing-card p {
  margin: 0 !important;
  font-size: 0.9rem;
  color: var(--vp-c-text-2);
  line-height: 1.4;
}
</style>

---

## Quick Navigation Roadmap

| Goal | Recommended Guide |
| :--- | :--- |
| **Understand Product Capabilities** | [Features Overview](../features-guide.md) |
| **Deploy with Docker Compose** | [Docker Deployment Guide](../deployment/docker.md) |
| **Deploy on Windows Server / IIS** | [IIS Deployment Guide](../deployment/windows-iis.md) |
| **Run Locally on macOS / Linux / Windows** | [Homelab Guide](../deployment/homelab.md) |
| **Verify Component Compatibility** | [Support Matrix](../support-matrix.md) |
