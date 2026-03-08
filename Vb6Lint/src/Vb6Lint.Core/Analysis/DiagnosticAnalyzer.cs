namespace Vb6Lint.Core.Analysis;

/// <summary>
/// Base class for all VB6 lint rules — directly mirrors Roslyn's abstract
/// <c>DiagnosticAnalyzer</c>.
///
/// Subclass this, override <see cref="SupportedDiagnostics"/> to advertise
/// the rule(s) you produce, and override <see cref="Initialize"/> to register
/// callbacks via the supplied <see cref="AnalysisContext"/>.
/// </summary>
public abstract class DiagnosticAnalyzer
{
    /// <summary>
    /// All <see cref="DiagnosticDescriptor"/>s that this analyzer may emit.
    /// </summary>
    public abstract IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }

    /// <summary>
    /// Called once per file by the host to let the analyzer register its
    /// node and/or compilation callbacks.
    /// </summary>
    public abstract void Initialize(AnalysisContext context);
}
