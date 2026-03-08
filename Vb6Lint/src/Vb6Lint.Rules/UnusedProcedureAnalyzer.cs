using Vb6Lint.Core.Analysis;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Rules;

/// <summary>
/// VB6007 — Unused private procedure
///
/// A <c>Private</c> Sub or Function that is never called within the same
/// module is dead code.  Public / Friend procedures are excluded because
/// they may be called from other files or raised as event handlers.
///
/// The check is based on identifier-name matching (lower-case comparison).
/// It cannot resolve overloads (VB6 has none) or dynamic dispatch, so false
/// positives are rare.  AddressOf references count as "usage".
/// </summary>
[DiagnosticAnalyzerMetadata("VB6007")]
public sealed class UnusedProcedureAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id:               "VB6007",
        title:            "Unused private procedure",
        messageFormat:    "Private {0} '{1}' is never called within this module.",
        category:         "Usage",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = new[] { Rule };

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterCompilationAction(AnalyzeModule);
    }

    private static void AnalyzeModule(CompilationAnalysisContext ctx)
    {
        var unit = ctx.Compilation;

        // Gather Private procedures
        var privateProcs = unit.Members
            .OfType<ProcedureDeclarationSyntax>()
            .Where(p => p.Access == AccessModifier.Private)
            .ToList();

        if (privateProcs.Count == 0) return;

        // Collect all identifier references from non-declaration code
        var calledNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in unit.Members)
        {
            if (member is ProcedureDeclarationSyntax proc)
            {
                foreach (var stmt in proc.Body)
                {
                    foreach (var id in stmt.DescendantsAndSelf().OfType<IdentifierExpressionSyntax>())
                        calledNames.Add(id.Name);
                    foreach (var inv in stmt.DescendantsAndSelf().OfType<InvocationExpressionSyntax>())
                    {
                        if (inv.Target is IdentifierExpressionSyntax tid) calledNames.Add(tid.Name);
                        if (inv.Target is MemberAccessExpressionSyntax ma) calledNames.Add(ma.Member);
                    }
                    // AddressOf references
                    foreach (var unary in stmt.DescendantsAndSelf().OfType<UnaryExpressionSyntax>())
                        if (unary.Operator == "AddressOf" && unary.Operand is IdentifierExpressionSyntax ao)
                            calledNames.Add(ao.Name);
                }
            }
        }

        foreach (var proc in privateProcs)
        {
            if (!calledNames.Contains(proc.Name))
            {
                var kind = proc.Kind switch
                {
                    ProcedureKind.Function    => "Function",
                    ProcedureKind.PropertyGet => "Property Get",
                    ProcedureKind.PropertyLet => "Property Let",
                    ProcedureKind.PropertySet => "Property Set",
                    _                         => "Sub",
                };
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.FilePath, proc.Line, proc.Column, kind, proc.Name));
            }
        }
    }
}
