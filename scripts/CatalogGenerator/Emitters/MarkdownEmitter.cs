using System.Text;
using CatalogGenerator.Models;

namespace CatalogGenerator.Emitters
{
    public static class MarkdownEmitter
    {
        public static string Emit(CatalogIndex index)
        {
            var sb = new StringBuilder();
            var all = index.GetAll().ToList();
            var positive = all.Where(r => r.Type == RequirementType.Positive).ToList();
            var guardrails = all.Where(r => r.Type == RequirementType.Negative).ToList();
            var categories = all.GroupBy(r => r.Category).OrderBy(g => g.Key).ToList();

            sb.AppendLine("# Software Requirements Specification (SRS) & Test Verification Catalog");
            sb.AppendLine();
            sb.AppendLine("> **Automated Verification Document:** Generated via `dotnet run --project scripts/CatalogGenerator`");
            sb.AppendLine($"> **Catalog Statistics:** **{all.Count} Requirements Verified** across **{all.Sum(r => r.Proofs.Count)} Test Proofs** ({positive.Count} Functional Capabilities, {guardrails.Count} Safety Guardrails).");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            sb.AppendLine("## 1. System Taxonomy & Verification Summary");
            sb.AppendLine();
            sb.AppendLine("| Category | Domain | Total Requirements | Positive Features | Guardrails / Fail-Closed | Verification Proofs |");
            sb.AppendLine("| :--- | :--- | :---: | :---: | :---: | :---: |");

            foreach (var cat in categories)
            {
                var catTotal = cat.Count();
                var catPos = cat.Count(r => r.Type == RequirementType.Positive);
                var catNeg = cat.Count(r => r.Type == RequirementType.Negative);
                var catProofs = cat.Sum(r => r.Proofs.Count);
                sb.AppendLine($"| **`{cat.Key}`** | {GetCategoryTitle(cat.Key)} | **{catTotal}** | {catPos} | {catNeg} | {catProofs} proofs |");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            sb.AppendLine("## 2. Functional Requirements (\"What the Application DOES\")");
            sb.AppendLine();

            foreach (var req in positive)
            {
                sb.AppendLine($"### `[{req.Id}]` {req.Description}");
                sb.AppendLine($"* **Category:** `{req.Category}` ({GetCategoryTitle(req.Category)})");
                sb.AppendLine($"* **Type:** Positive Feature Capability");
                sb.AppendLine($"* **Verification Proofs ({req.Proofs.Count}):**");
                foreach (var proof in req.Proofs)
                {
                    sb.AppendLine($"  - [{proof.Suite}] [`{proof.FilePath}#L{proof.LineNumber}`](https://github.com/spelech/model-context-gateway/blob/main/{proof.FilePath}#L{proof.LineNumber}) (`{proof.TestName}`)");
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 3. Boundary & Guardrail Invariants (\"What the Application DOES NOT DO\")");
            sb.AppendLine();
            sb.AppendLine("> [!IMPORTANT]");
            sb.AppendLine("> The following guardrails define strict security boundaries, fail-closed fault invariants, and forbidden application states.");
            sb.AppendLine();

            foreach (var req in guardrails)
            {
                sb.AppendLine($"### `[{req.Id}]` {req.Description}");
                sb.AppendLine($"* **Category:** `{req.Category}` ({GetCategoryTitle(req.Category)})");
                sb.AppendLine($"* **Type:** Negative / Safety Guardrail (Fail-Closed)");
                sb.AppendLine($"* **Verification Proofs ({req.Proofs.Count}):**");
                foreach (var proof in req.Proofs)
                {
                    sb.AppendLine($"  - [{proof.Suite}] [`{proof.FilePath}#L{proof.LineNumber}`](https://github.com/spelech/model-context-gateway/blob/main/{proof.FilePath}#L{proof.LineNumber}) (`{proof.TestName}`)");
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 4. Complete Verification Traceability Matrix");
            sb.AppendLine();
            sb.AppendLine("| Requirement ID | Type | Category | Description | Primary Proof | Suite |");
            sb.AppendLine("| :--- | :---: | :--- | :--- | :--- | :--- |");

            foreach (var req in all)
            {
                var p = req.Proofs.FirstOrDefault();
                var proofStr = p != null ? $"[`{Path.GetFileName(p.FilePath)}:L{p.LineNumber}`](https://github.com/spelech/model-context-gateway/blob/main/{p.FilePath}#L{p.LineNumber})" : "N/A";
                var suiteStr = p != null ? p.Suite : "N/A";
                var typeStr = req.Type == RequirementType.Positive ? "Positive" : "**Guardrail**";
                sb.AppendLine($"| `{req.Id}` | {typeStr} | `{req.Category}` | {req.Description} | {proofStr} | {suiteStr} |");
            }

            return sb.ToString();
        }

        private static string GetCategoryTitle(string cat) => cat.ToUpperInvariant() switch
        {
            "AUTH" => "Authentication, RBAC & Identity",
            "MCP" => "Model Context Protocol Engine & Tool Routing",
            "TRANS" => "Transports (SSE, HTTP, STDIO, Proxy)",
            "SEC" => "Secrets Providers & Encryption",
            "DB" => "Multi-Database Persistence & Migrations",
            "UI" => "Dashboard, Test Bench & Settings UI",
            "GUARD" => "Universal Safety & Fail-Closed Guardrails",
            _ => cat
        };
    }
}
