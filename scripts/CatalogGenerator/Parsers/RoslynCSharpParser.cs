using System.Xml.Linq;
using CatalogGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CatalogGenerator.Parsers
{
    public class RoslynCSharpParser
    {
        public void ParseDirectory(string directoryPath, CatalogIndex index, string? rootDir = null)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            var files = Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var normalized = file.Replace('\\', '/');
                // Skip bin, obj, AssemblyInfo, GlobalUsings
                if (normalized.Contains("/bin/") ||
                    normalized.Contains("/obj/") ||
                    normalized.EndsWith("/GlobalUsings.cs") ||
                    normalized.EndsWith("GlobalUsings.cs"))
                {
                    continue;
                }

                var pathForProof = !string.IsNullOrEmpty(rootDir)
                    ? Path.GetRelativePath(rootDir, file).Replace('\\', '/')
                    : normalized;

                var code = File.ReadAllText(file);
                ParseSource(pathForProof, code, index);
            }
        }

        public void ParseSource(string filePath, string sourceCode, CatalogIndex index)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetCompilationUnitRoot();

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methods)
            {
                var reqAttributes = method.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Where(a => a.Name.ToString().Contains("Requirement"))
                    .ToList();

                if (!reqAttributes.Any())
                {
                    continue;
                }

                var lineSpan = tree.GetLineSpan(method.Span);
                var lineNumber = lineSpan.StartLinePosition.Line + 1;
                var methodName = method.Identifier.Text;
                var xmlSummary = ExtractXmlSummary(method);

                foreach (var attr in reqAttributes)
                {
                    if (attr.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
                    {
                        continue;
                    }

                    var positionalArgs = attr.ArgumentList.Arguments.Where(a => a.NameEquals == null).ToList();
                    if (positionalArgs.Count == 0)
                    {
                        continue;
                    }

                    var idArg = ExtractStringValue(positionalArgs[0].Expression);
                    if (string.IsNullOrWhiteSpace(idArg))
                    {
                        continue;
                    }

                    var type = RequirementType.Positive;
                    var category = idArg.Contains('-') ? idArg.Substring(0, idArg.IndexOf('-')) : "GENERAL";
                    string? descArg = null;

                    if (positionalArgs.Count == 1)
                    {
                        descArg = xmlSummary ?? methodName;
                    }
                    else if (positionalArgs.Count == 2)
                    {
                        descArg = ExtractStringValue(positionalArgs[1].Expression);
                    }
                    else if (positionalArgs.Count == 3)
                    {
                        var secondArgStr = positionalArgs[1].Expression.ToString();
                        if (IsRequirementTypeExpression(secondArgStr))
                        {
                            type = ParseRequirementType(secondArgStr);
                            descArg = ExtractStringValue(positionalArgs[2].Expression);
                        }
                        else
                        {
                            category = ExtractStringValue(positionalArgs[1].Expression);
                            descArg = ExtractStringValue(positionalArgs[2].Expression);
                        }
                    }
                    else if (positionalArgs.Count >= 4)
                    {
                        category = ExtractStringValue(positionalArgs[1].Expression);
                        var typeArgStr = positionalArgs[2].Expression.ToString();
                        type = ParseRequirementType(typeArgStr);
                        descArg = ExtractStringValue(positionalArgs[3].Expression);
                    }

                    if (string.IsNullOrWhiteSpace(descArg))
                    {
                        descArg = xmlSummary ?? methodName;
                    }

                    foreach (var namedArg in attr.ArgumentList.Arguments.Where(a => a.NameEquals != null))
                    {
                        var name = namedArg.NameEquals!.Name.Identifier.Text;
                        var val = namedArg.Expression.ToString();

                        if (name == "Type")
                        {
                            type = ParseRequirementType(val);
                        }
                        else if (name == "Category")
                        {
                            category = ExtractStringValue(namedArg.Expression);
                        }
                        else if (name == "Description")
                        {
                            descArg = ExtractStringValue(namedArg.Expression);
                        }
                    }

                    var proof = new TestCaseProof(
                        Suite: "Backend xUnit",
                        FilePath: filePath.Replace('\\', '/'),
                        LineNumber: lineNumber,
                        TestName: methodName,
                        Details: xmlSummary
                    );

                    index.AddOrMergeProof(idArg, category, type, descArg, proof);
                }
            }
        }

        private string? ExtractXmlSummary(MethodDeclarationSyntax method)
        {
            var trivia = method.GetLeadingTrivia()
                .Select(t => t.GetStructure())
                .OfType<DocumentationCommentTriviaSyntax>()
                .FirstOrDefault();

            if (trivia == null)
            {
                return null;
            }

            var xmlString = trivia.ToString();
            try
            {
                var cleaned = string.Join("\n", xmlString.Split('\n')
                    .Select(l => l.Trim().TrimStart('/', ' ')));
                var elem = XElement.Parse("<doc>" + cleaned + "</doc>");
                var summary = elem.Element("summary")?.Value.Trim();
                return summary;
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractStringValue(ExpressionSyntax expression)
        {
            if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
            return expression.ToString().Trim('"');
        }

        private static bool IsRequirementTypeExpression(string text)
        {
            return text.Contains("RequirementType") || text.Contains("Positive") || text.Contains("Negative") || text.Contains("Guardrail");
        }

        private static RequirementType ParseRequirementType(string text)
        {
            if (text.Contains("Negative") || text.Contains("Guardrail"))
            {
                return RequirementType.Negative;
            }

            return RequirementType.Positive;
        }
    }
}
