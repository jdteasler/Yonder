using Vb6Lint.Core.Analysis;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Rules;

/// <summary>
/// VB6005 — Unused local variable / constant
///
/// Detects variables and constants declared with <c>Dim</c> or <c>Const</c>
/// inside a procedure body that are never read.  Only procedure-local
/// declarations are checked; module-level (Public/Private) variables are
/// excluded because their usage may be in other files.
///
/// A variable is considered "used" if its lower-case name appears in any
/// identifier reference within the same procedure after the declaration.
/// Assignment-only variables (written but never read) are also flagged
/// because they indicate dead code.
/// </summary>
[DiagnosticAnalyzerMetadata("VB6005")]
public sealed class UnusedVariableAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor RuleUnused = new(
        id:               "VB6005",
        title:            "Unused local variable",
        messageFormat:    "Local variable '{0}' in '{1}' is declared but never used.",
        category:         "Usage",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor RuleWriteOnly = new(
        id:               "VB6006",
        title:            "Write-only local variable",
        messageFormat:    "Local variable '{0}' in '{1}' is assigned but its value is never read.",
        category:         "Usage",
        defaultSeverity:  DiagnosticSeverity.Info);

    public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = new[] { RuleUnused, RuleWriteOnly };

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction<ProcedureDeclarationSyntax>(AnalyzeProcedure);
    }

    private static void AnalyzeProcedure(SyntaxNodeAnalysisContext ctx)
    {
        var proc = (ProcedureDeclarationSyntax)ctx.Node;

        // Gather all local declarations (Dim / Const / Static at statement level)
        var locals = new Dictionary<string, (VariableDeclaratorSyntax Declarator, VariableDeclarationSyntax Parent)>
            (StringComparer.OrdinalIgnoreCase);

        foreach (var stmt in proc.Body.OfType<VariableDeclarationSyntax>())
        {
            // Skip module-level modifiers (Public/Private would appear at module level, but be safe)
            if (stmt.Access is not (AccessModifier.Default or AccessModifier.Private)) continue;

            foreach (var dec in stmt.Declarators)
                locals.TryAdd(dec.Name, (dec, stmt));
        }

        if (locals.Count == 0) return;

        // Collect all identifiers referenced anywhere in the body that are NOT
        // in a pure-assignment LHS position.
        var reads  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stmt in proc.Body)
            CollectReadsAndWrites(stmt, reads, writes);

        // Also include the parameter names as "declared" — they count as reads
        // so we don't accidentally flag a parameter.
        foreach (var p in proc.Parameters) reads.Add(p.Name);

        // Report
        foreach (var (name, (dec, parent)) in locals)
        {
            bool isRead    = reads.Contains(name);
            bool isWritten = writes.Contains(name);

            if (!isRead && !isWritten)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(RuleUnused, ctx.FilePath, dec.Line, dec.Column, name, proc.Name));
            }
            else if (!isRead && isWritten)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(RuleWriteOnly, ctx.FilePath, dec.Line, dec.Column, name, proc.Name));
            }
        }
    }

    private static void CollectReadsAndWrites(SyntaxNode stmt, HashSet<string> reads, HashSet<string> writes)
    {
        // Assignments: the direct LHS identifier is a write; everything on the RHS is a read
        if (stmt is AssignmentStatementSyntax assign)
        {
            // Extract the root name from the LHS
            var lhsName = RootName(assign.Target);
            if (lhsName != null) writes.Add(lhsName);

            // Everything in the LHS beyond the root (e.g. subscripts) is a read
            foreach (var id in assign.Target.DescendantsAndSelf().OfType<IdentifierExpressionSyntax>()
                                     .Skip(1)) // skip the root itself
                reads.Add(id.Name);

            // RHS: all identifiers are reads
            foreach (var id in assign.Value.DescendantsAndSelf().OfType<IdentifierExpressionSyntax>())
                reads.Add(id.Name);

            return;
        }

        // Variable declarations with initialisers
        if (stmt is VariableDeclarationSyntax decl)
        {
            foreach (var dec in decl.Declarators)
            {
                if (dec.Initializer != null)
                    foreach (var id in dec.Initializer.DescendantsAndSelf().OfType<IdentifierExpressionSyntax>())
                        reads.Add(id.Name);
            }
            return;
        }

        // All other statements — every identifier reference counts as a read
        foreach (var id in stmt.DescendantsAndSelf().OfType<IdentifierExpressionSyntax>())
            reads.Add(id.Name);
    }

    private static string? RootName(ExpressionSyntax expr)
    {
        if (expr is IdentifierExpressionSyntax id) return id.Name;
        if (expr is MemberAccessExpressionSyntax ma) return RootName(ma.Object);
        if (expr is InvocationExpressionSyntax inv) return RootName(inv.Target);
        return null;
    }
}
