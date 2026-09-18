# 🏛️ Model Context Gateway (MCG) Architecture Specification

> [!NOTE] Documentation Reorganization Notice
> The Model Context Gateway architecture specification has been modularized and moved to the dedicated [**/architecture/**](architecture/index.md) documentation suite.
> 
> Please explore the dedicated architecture modules below or proceed directly to the [**Architecture Overview**](architecture/index.md).

---

## 📑 Modular Architecture Specification Suite

| Module | Document Link | Focus Area |
| :--- | :--- | :--- |
| **00. Architecture Overview** | [**Architecture Overview & Topology**](architecture/index.md) | Executive summary, 7 tenets, 7-layer architecture, and primary system topology diagram. |
| **01. Components** | [**Backend & Frontend Components**](architecture/components.md) | Clean Architecture boundaries (`Components/`, `Infrastructure/`, `Core/`), dependency inversion rules, and React 19 / Zustand state stores. |
| **02. Routing & Meta-Mode** | [**Protocol & Routing Engine**](architecture/routing-and-meta-mode.md) | Meta-Mode capability abstraction (`search_tools`, `execute_tool`), virtual proxying, JSON-RPC 2.0 multiplexing, and SSE/HTTP sequence diagrams. |
| **03. Authorization** | [**Authorization Pipeline & RBAC**](architecture/authorization-pipeline.md) | 4-stage hierarchical authorization pipeline, AppKey scope resolution, Admin SID bypass (`S-1-5-32-544`), and decision flowcharts. |
| **04. Transports** | [**Transports & Subprocesses**](architecture/transports-and-subprocesses.md) | `ITransport` strategy pattern, STDIO subprocess isolation, child process tree lifecycle and signal handling, and STDIO sequence diagram. |
| **05. Persistence & Secrets** | [**Database & Envelope Encryption**](architecture/database-and-encryption.md) | Multi-engine persistence dialect strategies (SQLite, MS SQL, MySQL), unified ERD (12 entities), AES-256-GCM envelope encryption, and secret resolution flowchart. |

---

*For broader context and operational instructions, see the [Project Overview](index.md) or [Official User Guide](user-guide.md).*
