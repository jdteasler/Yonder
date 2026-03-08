namespace Vb6Lint.Core.Analysis;

/// <summary>Mirrors Roslyn's DiagnosticSeverity enum.</summary>
public enum DiagnosticSeverity
{
    /// <summary>Not a problem; purely informational.</summary>
    Info,
    /// <summary>Something suspicious but not definitely wrong.</summary>
    Warning,
    /// <summary>A definite error that will or should cause a failure.</summary>
    Error,
}
