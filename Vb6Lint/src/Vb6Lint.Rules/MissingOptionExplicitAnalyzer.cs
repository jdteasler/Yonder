using Vb6Lint.Core.Analysis;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Rules;

/// <summary>
/// VB6001 — Missing Option Explicit
///
/// VB6 allows variables to be used without prior declaration when
/// <c>Option Explicit</c> is absent.  Implicit variable creation is a
/// common source of bugs (typos in variable names silently create new
/// variables).  This rule fires once per file when the directive is missing.
/// </summary>
[DiagnosticAnalyzerMetadata("VB6001")]
public sealed class MissingOptionExplicitAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id:               "VB6001",
        title:            "Missing 'Option Explicit'",
        messageFormat:    "Module '{0}' is missing 'Option Explicit'. " +
                          "Without it, undeclared variables are allowed and typos create silent bugs.",
        category:         "Correctness",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = new[] { Rule };

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext ctx)
    {
        bool hasOptionExplicit = ctx.Compilation.Members
            .OfType<OptionStatementSyntax>()
            .Any(o => string.Equals(o.OptionType, "Explicit", StringComparison.OrdinalIgnoreCase));

        if (!hasOptionExplicit)
        {
            var fileName = Path.GetFileName(ctx.FilePath);
            ctx.ReportDiagnostic(Diagnostic.Create(
                Rule,
                ctx.FilePath,
                line:   1,
                column: 1,
                fileName));
        }
    }
}
