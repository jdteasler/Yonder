using Vb6Lint.Core.Analysis;

namespace Vb6Lint.Rules;

/// <summary>
/// Factory that returns all built-in analyzers so the CLI (or any host)
/// can register them with a single call.
/// </summary>
public static class BuiltInRules
{
    public static IReadOnlyList<DiagnosticAnalyzer> All() =>
        new DiagnosticAnalyzer[]
        {
            new MissingOptionExplicitAnalyzer(),
            new OnErrorResumeNextAnalyzer(),
            new GoToAnalyzer(),
            new UnusedVariableAnalyzer(),
            new UnusedProcedureAnalyzer(),
            new ImplicitTypeCoercionAnalyzer(),
        };
}
