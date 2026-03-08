namespace Vb6Lint.Rules;

/// <summary>
/// Optional informational attribute applied to analyzer classes to make their
/// rule ID discoverable via reflection without constructing an instance.
/// (Mirrors the convention used in some Roslyn-based toolkits.)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DiagnosticAnalyzerMetadataAttribute : Attribute
{
    public string RuleId { get; }

    public DiagnosticAnalyzerMetadataAttribute(string ruleId) => RuleId = ruleId;
}
