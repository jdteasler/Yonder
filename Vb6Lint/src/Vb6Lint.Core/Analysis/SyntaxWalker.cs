using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Core.Analysis;

/// <summary>
/// Depth-first syntax tree visitor.  Override <see cref="Visit"/> or
/// <see cref="DefaultVisit"/> for cross-cutting behaviour, or simply call
/// <see cref="Walk"/> to trigger the registered node-action callbacks.
/// </summary>
public sealed class SyntaxWalker
{
    private readonly IReadOnlyList<(Type NodeType, Action<SyntaxNodeAnalysisContext> Callback)> _callbacks;
    private readonly List<Diagnostic> _sink;
    private readonly string _filePath;

    public SyntaxWalker(
        IReadOnlyList<(Type, Action<SyntaxNodeAnalysisContext>)> callbacks,
        List<Diagnostic> sink,
        string filePath)
    {
        _callbacks = callbacks;
        _sink      = sink;
        _filePath  = filePath;
    }

    public void Walk(SyntaxNode node)
    {
        // Fire matching callbacks for this node
        foreach (var (type, callback) in _callbacks)
        {
            if (type.IsInstanceOfType(node))
            {
                var ctx = new SyntaxNodeAnalysisContext(node, _filePath, _sink);
                callback(ctx);
            }
        }
        // Recurse into children
        foreach (var child in node.ChildNodes())
            Walk(child);
    }
}
