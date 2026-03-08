using Vb6Lint.Core.Analysis;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Rules;

/// <summary>
/// VB6002 — Unguarded 'On Error Resume Next'
///
/// <c>On Error Resume Next</c> suppresses all runtime errors and causes
/// execution to silently skip the failing statement.  This is safe only when
/// every subsequent statement explicitly checks <c>Err.Number</c> and clears
/// the error object.
///
/// This analyser fires when a procedure contains <c>On Error Resume Next</c>
/// but does not subsequently reference <c>Err</c> (or sub-properties like
/// <c>Err.Number</c> / <c>Err.Description</c>), indicating the error is
/// being swallowed without any handling.
/// </summary>
[DiagnosticAnalyzerMetadata("VB6002")]
public sealed class OnErrorResumeNextAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id:               "VB6002",
        title:            "Unguarded 'On Error Resume Next'",
        messageFormat:    "'On Error Resume Next' in '{0}' suppresses all errors. " +
                          "Check Err.Number after each operation that may fail, " +
                          "or use 'On Error GoTo <label>' for structured handling.",
        category:         "Correctness",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = new[] { Rule };

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction<ProcedureDeclarationSyntax>(AnalyzeProcedure);
    }

    private static void AnalyzeProcedure(SyntaxNodeAnalysisContext ctx)
    {
        var proc = (ProcedureDeclarationSyntax)ctx.Node;

        // Collect all On Error statements in this procedure body
        var onErrorNodes = proc.Body
            .SelectMany(n => n.DescendantsAndSelf())
            .OfType<OnErrorStatementSyntax>()
            .ToList();

        foreach (var onErr in onErrorNodes)
        {
            if (onErr.Kind != OnErrorKind.ResumeNext) continue;

            // Check whether "Err" is referenced anywhere after this point in the body
            bool errReferenced = IsErrReferencedAfter(proc.Body, onErr);

            if (!errReferenced)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    ctx.FilePath,
                    onErr.Line,
                    onErr.Column,
                    proc.Name));
            }
        }
    }

    private static bool IsErrReferencedAfter(IEnumerable<SyntaxNode> stmts, OnErrorStatementSyntax target)
    {
        bool seenTarget = false;
        foreach (var stmt in stmts)
        {
            if (stmt == target) { seenTarget = true; continue; }
            if (!seenTarget) continue;

            foreach (var id in stmt.DescendantsAndSelf().OfType<IdentifierExpressionSyntax>())
            {
                if (string.Equals(id.Name, "err", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            foreach (var ma in stmt.DescendantsAndSelf().OfType<MemberAccessExpressionSyntax>())
            {
                if (ma.Object is IdentifierExpressionSyntax id2 &&
                    string.Equals(id2.Name, "err", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }
}
