using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Core.Analysis;

// ════════════════════════════════════════════════════════════════════════════
// Context types — passed to analyzer callbacks (mirrors Roslyn's pattern)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Passed to <see cref="DiagnosticAnalyzer.Initialize"/>.
/// Analyzers register callbacks on this object.
/// </summary>
public sealed class AnalysisContext
{
    private readonly List<(Type NodeType, Action<SyntaxNodeAnalysisContext> Callback)> _nodeCallbacks = new();
    private readonly List<Action<CompilationAnalysisContext>> _compilationCallbacks = new();

    /// <summary>
    /// Register a callback that fires for every AST node of type
    /// <typeparamref name="TNode"/>.
    /// </summary>
    public void RegisterSyntaxNodeAction<TNode>(Action<SyntaxNodeAnalysisContext> action)
        where TNode : SyntaxNode
        => _nodeCallbacks.Add((typeof(TNode), action));

    /// <summary>
    /// Register a callback that fires once after the full file has been parsed.
    /// Useful for cross-statement analyses such as "unused variable".
    /// </summary>
    public void RegisterCompilationAction(Action<CompilationAnalysisContext> action)
        => _compilationCallbacks.Add(action);

    internal IReadOnlyList<(Type, Action<SyntaxNodeAnalysisContext>)> NodeCallbacks     => _nodeCallbacks;
    internal IReadOnlyList<Action<CompilationAnalysisContext>>        CompilationCallbacks => _compilationCallbacks;
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides context for a specific syntax-node callback.</summary>
public sealed class SyntaxNodeAnalysisContext
{
    private readonly List<Diagnostic> _sink;

    public SyntaxNode  Node     { get; }
    public string      FilePath { get; }

    internal SyntaxNodeAnalysisContext(SyntaxNode node, string filePath, List<Diagnostic> sink)
    {
        Node     = node;
        FilePath = filePath;
        _sink    = sink;
    }

    public void ReportDiagnostic(Diagnostic diagnostic) => _sink.Add(diagnostic);
}

// ────────────────────────────────────────────────────────────────────────────

/// <summary>Provides context for a compilation-end callback.</summary>
public sealed class CompilationAnalysisContext
{
    private readonly List<Diagnostic> _sink;

    public CompilationUnitSyntax Compilation { get; }
    public string                FilePath    { get; }

    internal CompilationAnalysisContext(CompilationUnitSyntax compilation, string filePath, List<Diagnostic> sink)
    {
        Compilation = compilation;
        FilePath    = filePath;
        _sink       = sink;
    }

    public void ReportDiagnostic(Diagnostic diagnostic) => _sink.Add(diagnostic);
}
