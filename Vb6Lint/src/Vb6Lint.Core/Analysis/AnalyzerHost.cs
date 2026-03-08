using Vb6Lint.Core.Lexer;
using Vb6Lint.Core.Parser;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Core.Analysis;

/// <summary>
/// Orchestrates the full pipeline for one source file:
/// lex → parse → walk AST → run compilation callbacks → return diagnostics.
///
/// Analogous to Roslyn's <c>Compilation</c> + <c>AnalyzerDriver</c> combined.
/// </summary>
public sealed class AnalyzerHost
{
    private readonly List<DiagnosticAnalyzer> _analyzers = new();

    public void AddAnalyzer(DiagnosticAnalyzer analyzer) => _analyzers.Add(analyzer);

    public void AddAnalyzers(IEnumerable<DiagnosticAnalyzer> analyzers)
    {
        foreach (var a in analyzers) _analyzers.Add(a);
    }

    /// <summary>
    /// Run all registered analyzers against a source text string.
    /// </summary>
    public AnalysisResult Analyze(string sourceText, string filePath = "")
    {
        // 1. Lex
        var lexer  = new Vb6Lexer(sourceText);
        var tokens = lexer.Tokenize();

        // 2. Parse
        var parser = new Vb6Parser(tokens);
        var tree   = parser.Parse(filePath);

        return Analyze(tree, parser.Errors, filePath);
    }

    /// <summary>
    /// Run all registered analyzers against a pre-built syntax tree.
    /// </summary>
    public AnalysisResult Analyze(CompilationUnitSyntax tree, IReadOnlyList<ParseError> parseErrors, string filePath = "")
    {
        var diagnostics = new List<Diagnostic>();

        // Convert parse errors to diagnostics
        foreach (var pe in parseErrors)
        {
            diagnostics.Add(Diagnostic.Create(
                WellKnownDescriptors.ParseError,
                filePath,
                pe.Line,
                pe.Column,
                pe.Message));
        }

        if (_analyzers.Count == 0) return new AnalysisResult(tree, diagnostics);

        // 3. Build per-analyzer contexts
        var contexts = new List<AnalysisContext>(_analyzers.Count);
        foreach (var analyzer in _analyzers)
        {
            var ctx = new AnalysisContext();
            analyzer.Initialize(ctx);
            contexts.Add(ctx);
        }

        // 4. Walk AST — dispatch node-action callbacks
        var allNodeCallbacks = contexts
            .SelectMany(c => c.NodeCallbacks)
            .ToList();

        if (allNodeCallbacks.Count > 0)
        {
            var walker = new SyntaxWalker(allNodeCallbacks, diagnostics, filePath);
            walker.Walk(tree);
        }

        // 5. Compilation-end callbacks
        foreach (var ctx in contexts)
        {
            foreach (var compAction in ctx.CompilationCallbacks)
            {
                var compCtx = new CompilationAnalysisContext(tree, filePath, diagnostics);
                compAction(compCtx);
            }
        }

        // Sort by line then column for stable, readable output
        diagnostics.Sort((a, b) =>
        {
            int c = a.Line.CompareTo(b.Line);
            return c != 0 ? c : a.Column.CompareTo(b.Column);
        });

        return new AnalysisResult(tree, diagnostics);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Well-known internal descriptors
// ════════════════════════════════════════════════════════════════════════════

internal static class WellKnownDescriptors
{
    public static readonly DiagnosticDescriptor ParseError = new(
        "VB6000",
        "Parse error",
        "The parser encountered unexpected input: {0}",
        "Syntax",
        DiagnosticSeverity.Error);
}

// ════════════════════════════════════════════════════════════════════════════
// Result type
// ════════════════════════════════════════════════════════════════════════════

public sealed class AnalysisResult
{
    public CompilationUnitSyntax     SyntaxTree  { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool HasErrors   => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public bool HasWarnings => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);

    public AnalysisResult(CompilationUnitSyntax tree, List<Diagnostic> diagnostics)
    {
        SyntaxTree  = tree;
        Diagnostics = diagnostics;
    }
}
