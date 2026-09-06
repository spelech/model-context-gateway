using CatalogGenerator.Emitters;
using CatalogGenerator.Models;
using CatalogGenerator.Parsers;

namespace CatalogGenerator
{
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" Model Context Gateway Software Requirements Catalog Engine ");
            Console.WriteLine("=================================================");

            var rootDir = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(rootDir, "ModelContextGateway.csproj")) && Directory.GetParent(rootDir) != null)
            {
                rootDir = Directory.GetParent(rootDir)!.FullName;
            }

            Console.WriteLine($"[INFO] Repository Root: {rootDir}");

            var verifyOnly = args.Contains("--verify-only") || args.Contains("--verify");
            var index = new CatalogIndex();

            // 1. Parse C# tests
            var csParser = new RoslynCSharpParser();
            var csTestDir = Path.Combine(rootDir, "ModelContextGateway.Tests");
            if (Directory.Exists(csTestDir))
            {
                Console.WriteLine($"[INFO] Scanning C# xUnit tests in {csTestDir}...");
                csParser.ParseDirectory(csTestDir, index, rootDir);
            }

            // 2. Parse Vitest tests
            var tsParser = new TypeScriptTestParser();
            var vitestDir = Path.Combine(rootDir, "frontend", "src", "test");
            if (Directory.Exists(vitestDir))
            {
                Console.WriteLine($"[INFO] Scanning Frontend Vitest tests in {vitestDir}...");
                tsParser.ParseDirectory(vitestDir, index, "Frontend Vitest", rootDir);
            }

            // 3. Parse Playwright tests
            var playwrightDir = Path.Combine(rootDir, "frontend", "e2e");
            if (Directory.Exists(playwrightDir))
            {
                Console.WriteLine($"[INFO] Scanning Playwright E2E tests in {playwrightDir}...");
                tsParser.ParseDirectory(playwrightDir, index, "Playwright E2E", rootDir);
            }

            var all = index.GetAll().ToList();
            Console.WriteLine($"[SUCCESS] Discovered {all.Count} requirements across {all.Sum(r => r.Proofs.Count)} proofs.");

            var mdOutput = MarkdownEmitter.Emit(index);
            var jsonOutput = JsonEmitter.Emit(index);

            var mdPath = Path.Combine(rootDir, "docs", "software-requirements-and-test-catalog.md");
            var jsonPath = Path.Combine(rootDir, "docs", "requirements-catalog.json");

            if (verifyOnly)
            {
                if (!File.Exists(mdPath) || !File.Exists(jsonPath))
                {
                    Console.Error.WriteLine("[ERROR] Verification failed: Requirement catalog files are missing!");
                    return 1;
                }

                var existingMd = File.ReadAllText(mdPath);
                if (existingMd.Trim() != mdOutput.Trim())
                {
                    Console.Error.WriteLine("[ERROR] Verification failed: docs/software-requirements-and-test-catalog.md is out of date! Run without --verify to update.");
                    return 1;
                }

                Console.WriteLine("[SUCCESS] Requirement catalog is completely up to date!");
                return 0;
            }

            Directory.CreateDirectory(Path.Combine(rootDir, "docs"));
            File.WriteAllText(mdPath, mdOutput);
            File.WriteAllText(jsonPath, jsonOutput);

            Console.WriteLine($"[UPDATED] Markdown catalog written to: {mdPath}");
            Console.WriteLine($"[UPDATED] JSON catalog written to:     {jsonPath}");

            return 0;
        }
    }
}
