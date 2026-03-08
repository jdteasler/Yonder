using Vb6Lint.Core.Analysis;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Rules;

/// <summary>
/// VB6003 — Avoid GoTo
///
/// <c>GoTo</c> creates unstructured control flow that is difficult to follow,
/// test, and maintain.  The only widely-accepted use of <c>GoTo</c> in VB6
/// is jumping to an error-handling label preceded by <c>On Error GoTo</c>.
///
/// This analyser emits a warning for every <c>GoTo</c> statement that is
/// <em>not</em> the target of a corresponding <c>On Error GoTo</c> in the
/// same procedure, i.e. for "raw" flow-control GoTos.
/// </summary>
[DiagnosticAnalyzerMetadata("VB6003")]
public sealed class GoToAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor RuleGoTo = new(
        id:               "VB6003",
        title:            "Avoid GoTo",
        messageFormat:    "Use of 'GoTo {0}' creates unstructured control flow. " +
                          "Replace with structured constructs (If/Else, Do/Loop, Exit Sub) where possible.",
        category:         "Style",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor RuleGoSub = new(
        id:               "VB6004",
        title:            "Avoid GoSub",
        messageFormat:    "'GoSub' is an obsolete subroutine-call mechanism. " +
                          "Replace with a proper Private Sub or Function.",
        category:         "Style",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = new[] { RuleGoTo, RuleGoSub };

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction<ProcedureDeclarationSyntax>(AnalyzeProcedure);
    }

    private static void AnalyzeProcedure(SyntaxNodeAnalysisContext ctx)
    {
        var proc = (ProcedureDeclarationSyntax)ctx.Node;

        // Collect error-handler labels declared via On Error GoTo <label>
        var errorLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var onErr in proc.Body.SelectMany(n => n.DescendantsAndSelf()).OfType<OnErrorStatementSyntax>())
        {
            if (onErr.Kind == OnErrorKind.GoToLabel && onErr.GotoLabel != null)
                errorLabels.Add(onErr.GotoLabel);
        }

        foreach (var goTo in proc.Body.SelectMany(n => n.DescendantsAndSelf()).OfType<GoToStatementSyntax>())
        {
            if (goTo.IsGoSub)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(RuleGoSub, ctx.FilePath, goTo.Line, goTo.Column, goTo.Label));
            }
            else
            {
                // Allow GoTo that targets an error-handler label
                if (!errorLabels.Contains(goTo.Label))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(RuleGoTo, ctx.FilePath, goTo.Line, goTo.Column, goTo.Label));
                }
            }
        }
    }
}
