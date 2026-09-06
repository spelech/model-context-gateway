using System.Text.RegularExpressions;
using CatalogGenerator.Models;

namespace CatalogGenerator.Parsers
{
    public class TypeScriptTestParser
    {
        private static readonly Regex JsDocTestRegex = new Regex(
            @"/\*\*(?<jsdoc>[\s\S]*?)\*/\s*(?:it|test|test\.skip|it\.skip|test\.only|it\.only|test\.fixme|it\.fixme)\s*\(\s*(?<q>['""`])(?<name>[\s\S]*?)\k<q>",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        public void ParseDirectory(string directoryPath, CatalogIndex index, string suiteName = "Frontend", string? rootDir = null)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (ext != ".ts" && ext != ".tsx" && ext != ".js")
                {
                    continue;
                }

                if (file.Contains("node_modules") || file.Contains(".next") || file.Contains("dist"))
                {
                    continue;
                }

                var pathForProof = !string.IsNullOrEmpty(rootDir)
                    ? Path.GetRelativePath(rootDir, file).Replace('\\', '/')
                    : file.Replace('\\', '/');

                var code = File.ReadAllText(file);
                ParseSource(pathForProof, code, index, suiteName);
            }
        }

        public void ParseSource(string filePath, string sourceCode, CatalogIndex index, string suiteName = "Frontend")
        {
            var matches = JsDocTestRegex.Matches(sourceCode);

            foreach (Match match in matches)
            {
                var jsdoc = match.Groups["jsdoc"].Value;
                var testName = match.Groups["name"].Value.Trim();

                var idMatch = Regex.Match(jsdoc, @"@(?:id|requirement|req)\s+([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (!idMatch.Success)
                {
                    continue;
                }

                var id = idMatch.Groups[1].Value.Trim();

                var catMatch = Regex.Match(jsdoc, @"@category\s+([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
                var category = catMatch.Success ? catMatch.Groups[1].Value.Trim() : (id.Contains('-') ? id.Substring(0, id.IndexOf('-')) : "UI");

                var typeMatch = Regex.Match(jsdoc, @"@type\s+([A-Za-z0-9\-_/]+)", RegexOptions.IgnoreCase);
                var typeStr = typeMatch.Success ? typeMatch.Groups[1].Value.ToLowerInvariant() : "positive";
                var type = (typeStr.Contains("neg") || typeStr.Contains("guard")) ? RequirementType.Negative : RequirementType.Positive;

                var descMatch = Regex.Match(jsdoc, @"@desc(?:ription)?\s+([^\r\n]+)", RegexOptions.IgnoreCase);
                var desc = descMatch.Success ? descMatch.Groups[1].Value.Trim().TrimEnd('*', '/').Trim() : testName;

                // Find line number where JSDoc begins
                var lineNumber = sourceCode.Substring(0, match.Index).Count(c => c == '\n') + 1;

                var proof = new TestCaseProof(
                    Suite: suiteName,
                    FilePath: filePath.Replace('\\', '/'),
                    LineNumber: lineNumber,
                    TestName: testName
                );

                index.AddOrMergeProof(id, category, type, desc, proof);
            }
        }
    }
}
