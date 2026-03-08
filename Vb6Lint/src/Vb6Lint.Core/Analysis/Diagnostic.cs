namespace Vb6Lint.Core.Analysis;

/// <summary>
/// A single reported diagnostic instance — analogous to Roslyn's
/// <c>Diagnostic</c> class.
/// </summary>
public sealed class Diagnostic
{
    public DiagnosticDescriptor Descriptor { get; }
    public DiagnosticSeverity   Severity   { get; }
    public string               FilePath   { get; }
    public int                  Line       { get; }   // 1-based
    public int                  Column     { get; }   // 1-based
    public string               Message    { get; }

    private Diagnostic(
        DiagnosticDescriptor descriptor,
        DiagnosticSeverity   severity,
        string               filePath,
        int                  line,
        int                  column,
        string               message)
    {
        Descriptor = descriptor;
        Severity   = severity;
        FilePath   = filePath;
        Line       = line;
        Column     = column;
        Message    = message;
    }

    // ── factory helpers ──────────────────────────────────────────────────────

    public static Diagnostic Create(
        DiagnosticDescriptor descriptor,
        string               filePath,
        int                  line,
        int                  column,
        params object[]      messageArgs)
    {
        var message = messageArgs.Length > 0
            ? string.Format(descriptor.MessageFormat, messageArgs)
            : descriptor.MessageFormat;

        return new Diagnostic(descriptor, descriptor.DefaultSeverity, filePath, line, column, message);
    }

    // ── MSBuild / CI-compatible one-line output ───────────────────────────────

    /// <summary>
    /// Formats the diagnostic in MSBuild-compatible format:
    /// <c>file(line,col): severity RuleId: message</c>
    /// </summary>
    public string ToMsBuildString()
    {
        var file = string.IsNullOrWhiteSpace(FilePath) ? "(unknown)" : FilePath;
        var sev  = Severity switch
        {
            DiagnosticSeverity.Error   => "error",
            DiagnosticSeverity.Warning => "warning",
            _                          => "info",
        };
        return $"{file}({Line},{Column}): {sev} {Descriptor.Id}: {Message}";
    }

    public override string ToString() => ToMsBuildString();
}
