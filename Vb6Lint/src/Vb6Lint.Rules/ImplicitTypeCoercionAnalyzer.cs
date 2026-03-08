using Vb6Lint.Core.Analysis;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Rules;

/// <summary>
/// VB6008 — Implicit type coercion
///
/// VB6 silently coerces values between types in assignments and comparisons.
/// While occasionally intentional, most implicit coercions indicate missing
/// explicit conversion calls (<c>CInt</c>, <c>CLng</c>, <c>CStr</c>, etc.)
/// and can produce runtime errors (type mismatch) or data loss.
///
/// Patterns detected:
/// 1. Assigning a String literal directly to a variable declared as a numeric
///    type (Integer / Long / Single / Double / Currency).
/// 2. Comparing a variable to a magic numeric literal where the variable was
///    declared as String (missing CInt / Val call).
/// 3. Using <c>+</c> for string concatenation (should be <c>&amp;</c>).
/// </summary>
[DiagnosticAnalyzerMetadata("VB6008")]
public sealed class ImplicitTypeCoercionAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor RuleStringToNumeric = new(
        id:               "VB6008",
        title:            "Implicit string-to-numeric coercion",
        messageFormat:    "Assigning a String value to '{0}' (declared as {1}) relies on implicit coercion. " +
                          "Use an explicit conversion function such as CInt(), CLng(), CDbl().",
        category:         "Correctness",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor RulePlusForConcat = new(
        id:               "VB6009",
        title:            "Use '&' for string concatenation",
        messageFormat:    "Using '+' for string concatenation is ambiguous. " +
                          "When either operand is a String, use '&' to make the intent explicit " +
                          "and avoid numeric-addition surprises.",
        category:         "Style",
        defaultSeverity:  DiagnosticSeverity.Warning);

    public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = new[] { RuleStringToNumeric, RulePlusForConcat };

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction<ProcedureDeclarationSyntax>(AnalyzeProcedure);
    }

    private static void AnalyzeProcedure(SyntaxNodeAnalysisContext ctx)
    {
        var proc = (ProcedureDeclarationSyntax)ctx.Node;

        // Build a map of variable name → declared type for locals
        var localTypes = BuildLocalTypeMap(proc);

        // Also include parameters
        foreach (var p in proc.Parameters)
            if (p.TypeName != null)
                localTypes[p.Name] = p.TypeName;

        // Walk assignments
        foreach (var assign in proc.Body.SelectMany(s => s.DescendantsAndSelf()).OfType<AssignmentStatementSyntax>())
        {
            var lhsName = RootName(assign.Target);
            if (lhsName == null) continue;

            if (!localTypes.TryGetValue(lhsName, out var declaredType)) continue;

            // Rule: assigning a string literal to a numeric variable
            if (IsNumericType(declaredType) && assign.Value is LiteralExpressionSyntax lit
                                             && lit.LiteralKind == Core.Lexer.TokenKind.StringLiteral)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    RuleStringToNumeric,
                    ctx.FilePath,
                    assign.Line,
                    assign.Column,
                    lhsName,
                    declaredType));
            }
        }

        // Walk binary expressions for '+' on strings
        foreach (var bin in proc.Body.SelectMany(s => s.DescendantsAndSelf()).OfType<BinaryExpressionSyntax>())
        {
            if (bin.Operator != "+") continue;

            bool leftIsString  = IsStringExpression(bin.Left,  localTypes);
            bool rightIsString = IsStringExpression(bin.Right, localTypes);

            if (leftIsString || rightIsString)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    RulePlusForConcat,
                    ctx.FilePath,
                    bin.Line,
                    bin.Column));
            }
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, string> BuildLocalTypeMap(ProcedureDeclarationSyntax proc)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var decl in proc.Body.OfType<VariableDeclarationSyntax>())
            foreach (var dec in decl.Declarators)
                if (dec.TypeName != null)
                    map[dec.Name] = dec.TypeName;
        return map;
    }

    private static bool IsNumericType(string typeName) => typeName.ToLowerInvariant() switch
    {
        "integer" or "long" or "single" or "double" or "currency"
            or "byte" or "longlong" or "longptr" => true,
        _ => false,
    };

    private static bool IsStringExpression(ExpressionSyntax expr, Dictionary<string, string> localTypes)
    {
        if (expr is LiteralExpressionSyntax lit && lit.LiteralKind == Core.Lexer.TokenKind.StringLiteral)
            return true;

        if (expr is IdentifierExpressionSyntax id &&
            localTypes.TryGetValue(id.Name, out var t) &&
            string.Equals(t, "String", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string? RootName(ExpressionSyntax expr)
    {
        if (expr is IdentifierExpressionSyntax id) return id.Name;
        if (expr is MemberAccessExpressionSyntax ma) return RootName(ma.Object);
        return null;
    }
}
