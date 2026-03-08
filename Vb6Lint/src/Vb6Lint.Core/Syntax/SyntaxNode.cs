using Vb6Lint.Core.Lexer;

namespace Vb6Lint.Core.Syntax;

// ════════════════════════════════════════════════════════════════════════════
// Base
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Base class for all VB6 AST nodes.  Every node records the source location
/// of its first token so diagnostics can cite line/column precisely.
/// </summary>
public abstract class SyntaxNode
{
    public int    Line     { get; protected set; }
    public int    Column   { get; protected set; }
    public string FilePath { get; set; } = string.Empty;

    protected SyntaxNode() { }
    protected SyntaxNode(Token firstToken)
    {
        Line   = firstToken.Line;
        Column = firstToken.Column;
    }

    /// <summary>Returns all direct child nodes for tree-walk visitors.</summary>
    public abstract IEnumerable<SyntaxNode> ChildNodes();
}

// ════════════════════════════════════════════════════════════════════════════
// Top-level
// ════════════════════════════════════════════════════════════════════════════

/// <summary>Root of the AST — represents one VB6 source file.</summary>
public sealed class CompilationUnitSyntax : SyntaxNode
{
    /// <summary>Parsed module kind inferred from file extension / header.</summary>
    public ModuleKind Kind { get; set; } = ModuleKind.StandardModule;

    /// <summary>All top-level members: options, declarations, procedures.</summary>
    public List<SyntaxNode> Members { get; } = new();

    public CompilationUnitSyntax() { Line = 1; Column = 1; }

    public override IEnumerable<SyntaxNode> ChildNodes() => Members;
}

public enum ModuleKind { StandardModule, ClassModule, FormModule }

// ════════════════════════════════════════════════════════════════════════════
// Module attributes and options
// ════════════════════════════════════════════════════════════════════════════

public sealed class OptionStatementSyntax : SyntaxNode
{
    public string OptionType { get; set; } = string.Empty; // "Explicit", "Base", "Compare"
    public string? OptionValue { get; set; }               // null, "1", "Text", "Binary"

    public OptionStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

public sealed class AttributeStatementSyntax : SyntaxNode
{
    public string Name  { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public AttributeStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ════════════════════════════════════════════════════════════════════════════
// Declarations
// ════════════════════════════════════════════════════════════════════════════

public enum AccessModifier { Default, Public, Private, Friend, Global }

public sealed class VariableDeclaratorSyntax : SyntaxNode
{
    public string  Name       { get; set; } = string.Empty;
    public string? TypeName   { get; set; }
    public bool    IsNew      { get; set; }
    public bool    WithEvents { get; set; }
    public ExpressionSyntax? Initializer { get; set; } // only valid for Const

    public VariableDeclaratorSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (Initializer != null) yield return Initializer;
    }
}

public sealed class VariableDeclarationSyntax : SyntaxNode
{
    public AccessModifier          Access      { get; set; }
    public bool                    IsConst     { get; set; }
    public bool                    IsStatic    { get; set; }
    public List<VariableDeclaratorSyntax> Declarators { get; } = new();

    public VariableDeclarationSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Declarators;
}

public sealed class ParameterSyntax : SyntaxNode
{
    public string  Name       { get; set; } = string.Empty;
    public string? TypeName   { get; set; }
    public bool    ByVal      { get; set; }
    public bool    ByRef      { get; set; }
    public bool    Optional   { get; set; }
    public bool    ParamArray { get; set; }
    public ExpressionSyntax? DefaultValue { get; set; }

    public ParameterSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (DefaultValue != null) yield return DefaultValue;
    }
}

public enum ProcedureKind { Sub, Function, PropertyGet, PropertyLet, PropertySet }

public sealed class ProcedureDeclarationSyntax : SyntaxNode
{
    public AccessModifier        Access     { get; set; }
    public ProcedureKind         Kind       { get; set; }
    public string                Name       { get; set; } = string.Empty;
    public bool                  IsStatic   { get; set; }
    public List<ParameterSyntax> Parameters { get; } = new();
    public string?               ReturnType { get; set; } // Function/Property Get
    public List<SyntaxNode>      Body       { get; } = new();

    public ProcedureDeclarationSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Parameters.Cast<SyntaxNode>().Concat(Body);
}

public sealed class ExternalDeclarationSyntax : SyntaxNode
{
    public AccessModifier        Access       { get; set; }
    public bool                  IsFunction   { get; set; }
    public string                Name         { get; set; } = string.Empty;
    public string                LibName      { get; set; } = string.Empty;
    public string?               Alias        { get; set; }
    public List<ParameterSyntax> Parameters   { get; } = new();
    public string?               ReturnType   { get; set; }

    public ExternalDeclarationSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Parameters;
}

public sealed class TypeDeclarationSyntax : SyntaxNode
{
    public AccessModifier                   Access  { get; set; }
    public string                           Name    { get; set; } = string.Empty;
    public List<VariableDeclarationSyntax>  Fields  { get; } = new();

    public TypeDeclarationSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Fields;
}

public sealed class EnumDeclarationSyntax : SyntaxNode
{
    public AccessModifier              Access   { get; set; }
    public string                      Name     { get; set; } = string.Empty;
    public List<EnumMemberSyntax>      Members  { get; } = new();

    public EnumDeclarationSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Members;
}

public sealed class EnumMemberSyntax : SyntaxNode
{
    public string            Name  { get; set; } = string.Empty;
    public ExpressionSyntax? Value { get; set; }

    public EnumMemberSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (Value != null) yield return Value;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Statements
// ════════════════════════════════════════════════════════════════════════════

public abstract class StatementSyntax : SyntaxNode
{
    protected StatementSyntax(Token tok) : base(tok) { }
}

// ── Assignment ────────────────────────────────────────────────────────────

public sealed class AssignmentStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Target { get; set; } = null!;
    public ExpressionSyntax Value  { get; set; } = null!;
    public bool             IsSet  { get; set; } // Set x = ...
    public bool             IsLet  { get; set; } // Let x = ...

    public AssignmentStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() { yield return Target; yield return Value; }
}

// ── Call ──────────────────────────────────────────────────────────────────

public sealed class CallStatementSyntax : StatementSyntax
{
    public ExpressionSyntax          Target    { get; set; } = null!;
    public List<ExpressionSyntax>    Arguments { get; } = new();
    public bool                      IsCallKeyword { get; set; }

    public CallStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Enumerable.Repeat(Target, 1).Concat(Arguments.Cast<SyntaxNode>());
}

// ── On Error ──────────────────────────────────────────────────────────────

public enum OnErrorKind { ResumeNext, GoTo0, GoToLabel }

public sealed class OnErrorStatementSyntax : StatementSyntax
{
    public OnErrorKind Kind       { get; set; }
    public string?     GotoLabel  { get; set; } // for GoToLabel variant

    public OnErrorStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── Resume ────────────────────────────────────────────────────────────────

public sealed class ResumeStatementSyntax : StatementSyntax
{
    public bool    IsNext  { get; set; }
    public string? Label   { get; set; }

    public ResumeStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── GoTo / GoSub ─────────────────────────────────────────────────────────

public sealed class GoToStatementSyntax : StatementSyntax
{
    public string Label   { get; set; } = string.Empty;
    public bool   IsGoSub { get; set; }

    public GoToStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── Label ─────────────────────────────────────────────────────────────────

public sealed class LabelStatementSyntax : StatementSyntax
{
    public string Name { get; set; } = string.Empty;

    public LabelStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── If ────────────────────────────────────────────────────────────────────

public sealed class IfClauseSyntax : SyntaxNode
{
    public ExpressionSyntax Condition { get; set; } = null!;
    public List<SyntaxNode> Body      { get; } = new();

    public IfClauseSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Enumerable.Repeat(Condition, 1).Concat(Body.Cast<SyntaxNode>());
}

public sealed class IfStatementSyntax : StatementSyntax
{
    public ExpressionSyntax  Condition    { get; set; } = null!;
    public List<SyntaxNode>  ThenBody     { get; } = new();
    public List<IfClauseSyntax> ElseIfs   { get; } = new();
    public List<SyntaxNode>? ElseBody     { get; set; }
    public bool              IsSingleLine { get; set; }

    public IfStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Condition;
        foreach (var n in ThenBody) yield return n;
        foreach (var c in ElseIfs)  yield return c;
        if (ElseBody != null) foreach (var n in ElseBody) yield return n;
    }
}

// ── Select Case ───────────────────────────────────────────────────────────

public sealed class CaseClauseSyntax : SyntaxNode
{
    public List<ExpressionSyntax> Patterns { get; } = new(); // empty = Case Else
    public bool                   IsElse   { get; set; }
    public List<SyntaxNode>       Body     { get; } = new();

    public CaseClauseSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Patterns.Cast<SyntaxNode>().Concat(Body);
}

public sealed class SelectStatementSyntax : StatementSyntax
{
    public ExpressionSyntax    TestExpression { get; set; } = null!;
    public List<CaseClauseSyntax> Cases       { get; } = new();

    public SelectStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Enumerable.Repeat(TestExpression, 1).Concat(Cases.Cast<SyntaxNode>());
}

// ── For / Next ────────────────────────────────────────────────────────────

public sealed class ForStatementSyntax : StatementSyntax
{
    public string            Counter  { get; set; } = string.Empty;
    public ExpressionSyntax  Start    { get; set; } = null!;
    public ExpressionSyntax  Limit    { get; set; } = null!;
    public ExpressionSyntax? Step     { get; set; }
    public List<SyntaxNode>  Body     { get; } = new();

    public ForStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Start;
        yield return Limit;
        if (Step != null) yield return Step;
        foreach (var n in Body) yield return n;
    }
}

public sealed class ForEachStatementSyntax : StatementSyntax
{
    public string            Iterator   { get; set; } = string.Empty;
    public ExpressionSyntax  Collection { get; set; } = null!;
    public List<SyntaxNode>  Body       { get; } = new();

    public ForEachStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Enumerable.Repeat(Collection, 1).Concat(Body.Cast<SyntaxNode>());
}

// ── While / Wend ─────────────────────────────────────────────────────────

public sealed class WhileStatementSyntax : StatementSyntax
{
    public ExpressionSyntax Condition { get; set; } = null!;
    public List<SyntaxNode> Body      { get; } = new();

    public WhileStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Enumerable.Repeat(Condition, 1).Concat(Body.Cast<SyntaxNode>());
}

// ── Do / Loop ─────────────────────────────────────────────────────────────

public enum DoLoopConditionPlacement { None, AtTop, AtBottom }

public sealed class DoStatementSyntax : StatementSyntax
{
    public DoLoopConditionPlacement ConditionPlacement { get; set; }
    public bool                     IsUntil            { get; set; } // true=Until, false=While
    public ExpressionSyntax?        Condition          { get; set; }
    public List<SyntaxNode>         Body               { get; } = new();

    public DoStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (Condition != null) yield return Condition;
        foreach (var n in Body) yield return n;
    }
}

// ── With ─────────────────────────────────────────────────────────────────

public sealed class WithStatementSyntax : StatementSyntax
{
    public ExpressionSyntax  Object  { get; set; } = null!;
    public List<SyntaxNode>  Body    { get; } = new();

    public WithStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Enumerable.Repeat(Object, 1).Concat(Body.Cast<SyntaxNode>());
}

// ── Exit ─────────────────────────────────────────────────────────────────

public sealed class ExitStatementSyntax : StatementSyntax
{
    public string Target { get; set; } = string.Empty; // "Sub", "Function", "For", "Do", "Property"

    public ExitStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── Return ───────────────────────────────────────────────────────────────

public sealed class ReturnStatementSyntax : StatementSyntax
{
    public ReturnStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── Stop ─────────────────────────────────────────────────────────────────

public sealed class StopStatementSyntax : StatementSyntax
{
    public StopStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── RaiseEvent ───────────────────────────────────────────────────────────

public sealed class RaiseEventStatementSyntax : StatementSyntax
{
    public string                  EventName { get; set; } = string.Empty;
    public List<ExpressionSyntax>  Arguments { get; } = new();

    public RaiseEventStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Arguments;
}

// ── Error statement ───────────────────────────────────────────────────────

public sealed class ErrorStatementSyntax : StatementSyntax
{
    public ExpressionSyntax ErrorNumber { get; set; } = null!;

    public ErrorStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() { yield return ErrorNumber; }
}

// ── Empty statement (blank line between statements) ───────────────────────

public sealed class EmptyStatementSyntax : StatementSyntax
{
    public EmptyStatementSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ════════════════════════════════════════════════════════════════════════════
// Expressions
// ════════════════════════════════════════════════════════════════════════════

public abstract class ExpressionSyntax : SyntaxNode
{
    protected ExpressionSyntax(Token tok) : base(tok) { }
}

// ── Literal ───────────────────────────────────────────────────────────────

public sealed class LiteralExpressionSyntax : ExpressionSyntax
{
    public TokenKind LiteralKind { get; set; }
    public string    RawText     { get; set; } = string.Empty;
    public object?   Value       { get; set; }

    public LiteralExpressionSyntax(Token tok) : base(tok)
    {
        LiteralKind = tok.Kind;
        RawText     = tok.Text;
    }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── Identifier ───────────────────────────────────────────────────────────

public sealed class IdentifierExpressionSyntax : ExpressionSyntax
{
    public string Name { get; set; } = string.Empty;

    public IdentifierExpressionSyntax(Token tok) : base(tok) { Name = tok.Value; }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── Member access  obj.Member ─────────────────────────────────────────────

public sealed class MemberAccessExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Object { get; set; } = null!;
    public string            Member { get; set; } = string.Empty;

    public MemberAccessExpressionSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() { yield return Object; }
}

// ── Invocation  expr(args) ────────────────────────────────────────────────

public sealed class InvocationExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax         Target    { get; set; } = null!;
    public List<ExpressionSyntax>   Arguments { get; } = new();

    public InvocationExpressionSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
        => Enumerable.Repeat(Target, 1).Concat(Arguments.Cast<SyntaxNode>());
}

// ── Binary expression ─────────────────────────────────────────────────────

public sealed class BinaryExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Left     { get; set; } = null!;
    public string            Operator { get; set; } = string.Empty;
    public ExpressionSyntax Right    { get; set; } = null!;

    public BinaryExpressionSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Left;
        yield return Right;
    }
}

// ── Unary expression ──────────────────────────────────────────────────────

public sealed class UnaryExpressionSyntax : ExpressionSyntax
{
    public string            Operator { get; set; } = string.Empty;
    public ExpressionSyntax  Operand  { get; set; } = null!;

    public UnaryExpressionSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() { yield return Operand; }
}

// ── New object  New Foo ───────────────────────────────────────────────────

public sealed class NewObjectExpressionSyntax : ExpressionSyntax
{
    public string TypeName { get; set; } = string.Empty;

    public NewObjectExpressionSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() => Enumerable.Empty<SyntaxNode>();
}

// ── TypeOf … Is … ────────────────────────────────────────────────────────

public sealed class TypeOfExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Object   { get; set; } = null!;
    public string            TypeName { get; set; } = string.Empty;

    public TypeOfExpressionSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() { yield return Object; }
}

// ── Parenthesised ─────────────────────────────────────────────────────────

public sealed class ParenthesisedExpressionSyntax : ExpressionSyntax
{
    public ExpressionSyntax Inner { get; set; } = null!;

    public ParenthesisedExpressionSyntax(Token tok) : base(tok) { }
    public override IEnumerable<SyntaxNode> ChildNodes() { yield return Inner; }
}
