using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Core.Analysis;

/// <summary>
/// Extension methods for traversing and querying <see cref="SyntaxNode"/> trees.
/// </summary>
public static class SyntaxTreeExtensions
{
    /// <summary>Enumerates every node in the subtree rooted at <paramref name="root"/> (depth-first).</summary>
    public static IEnumerable<SyntaxNode> DescendantsAndSelf(this SyntaxNode root)
    {
        var stack = new Stack<SyntaxNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            // Push children in reverse order so left-to-right DFS ordering is preserved
            var children = node.ChildNodes().ToList();
            for (int i = children.Count - 1; i >= 0; i--)
                stack.Push(children[i]);
        }
    }

    /// <summary>Enumerates every node of type <typeparamref name="T"/> in the subtree.</summary>
    public static IEnumerable<T> Descendants<T>(this SyntaxNode root) where T : SyntaxNode
        => root.DescendantsAndSelf().OfType<T>();

    /// <summary>
    /// Returns all identifiers referenced in an expression subtree.
    /// Useful for "is this variable used?" checks.
    /// </summary>
    public static IEnumerable<string> ReferencedNames(this SyntaxNode root)
    {
        foreach (var id in root.Descendants<IdentifierExpressionSyntax>())
            yield return id.Name;
        foreach (var ma in root.Descendants<MemberAccessExpressionSyntax>())
            yield return ma.Member;
        foreach (var inv in root.Descendants<InvocationExpressionSyntax>())
        {
            if (inv.Target is IdentifierExpressionSyntax id2) yield return id2.Name;
        }
    }
}
