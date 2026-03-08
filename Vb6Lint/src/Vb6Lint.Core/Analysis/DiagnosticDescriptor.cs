namespace Vb6Lint.Core.Analysis;

/// <summary>
/// Immutable metadata for a diagnostic rule — analogous to Roslyn's
/// <c>DiagnosticDescriptor</c>.  Every rule defines exactly one descriptor
/// and re-uses it across all reported instances.
/// </summary>
public sealed class DiagnosticDescriptor
{
    /// <summary>
    /// Unique rule identifier, e.g. "VB6001".
    /// Follows the pattern: VB6 + four-digit number.
    /// </summary>
    public string Id { get; }

    /// <summary>Short human-readable title (one line, no period).</summary>
    public string Title { get; }

    /// <summary>
    /// Message format string.  Supports {0}, {1} … placeholders that are
    /// filled in at report time.
    /// </summary>
    public string MessageFormat { get; }

    /// <summary>Broad category name (e.g. "Style", "Correctness", "Usage").</summary>
    public string Category { get; }

    /// <summary>Default severity.  Callers may override per-instance.</summary>
    public DiagnosticSeverity DefaultSeverity { get; }

    /// <summary>
    /// Help link that points to rule documentation (may be empty).
    /// </summary>
    public string HelpLinkUri { get; }

    /// <summary>Whether the rule is enabled by default.</summary>
    public bool IsEnabledByDefault { get; }

    public DiagnosticDescriptor(
        string id,
        string title,
        string messageFormat,
        string category,
        DiagnosticSeverity defaultSeverity,
        bool isEnabledByDefault = true,
        string helpLinkUri = "")
    {
        Id                 = id ?? throw new ArgumentNullException(nameof(id));
        Title              = title ?? throw new ArgumentNullException(nameof(title));
        MessageFormat      = messageFormat ?? throw new ArgumentNullException(nameof(messageFormat));
        Category           = category ?? throw new ArgumentNullException(nameof(category));
        DefaultSeverity    = defaultSeverity;
        IsEnabledByDefault = isEnabledByDefault;
        HelpLinkUri        = helpLinkUri;
    }
}
