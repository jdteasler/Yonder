using Vb6Lint.Core.Lexer;
using Vb6Lint.Core.Syntax;

namespace Vb6Lint.Core.Parser;

/// <summary>
/// Recursive-descent parser for VB6 source files.
///
/// VB6 is a statement-oriented, line-delimited language.  Each logical line is
/// terminated by a <see cref="TokenKind.NewLine"/> or the end of file.  The
/// colon ':' acts as a secondary statement separator within the same physical
/// line.  Line-continuations have already been stripped by the lexer.
///
/// Error recovery: on a parse error the parser emits a <see cref="ParseError"/>
/// and skips tokens until the next NewLine so it can continue with subsequent
/// statements.  This ensures the rest of the file is still analysed.
/// </summary>
public sealed class Vb6Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;

    public List<ParseError> Errors { get; } = new();

    public Vb6Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    // ── public entry point ───────────────────────────────────────────────────

    public CompilationUnitSyntax Parse(string filePath = "")
    {
        var unit = new CompilationUnitSyntax { FilePath = filePath };

        SkipNewLines();
        while (!AtEnd())
        {
            var member = ParseTopLevelMember();
            if (member != null)
            {
                member.FilePath = filePath;
                unit.Members.Add(member);
            }
            SkipNewLines();
        }
        return unit;
    }

    // ── token helpers ────────────────────────────────────────────────────────

    private Token Current   => _pos < _tokens.Count ? _tokens[_pos]   : _tokens[^1];
    private Token LookAhead(int n = 1) => (_pos + n) < _tokens.Count ? _tokens[_pos + n] : _tokens[^1];

    private bool AtEnd() => Current.Kind == TokenKind.EndOfFile;
    private bool IsNewLine(Token t) => t.Kind is TokenKind.NewLine or TokenKind.Colon;

    private Token Advance() { var t = Current; if (_pos < _tokens.Count - 1) _pos++; return t; }

    private Token Consume(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            RecordError($"Expected {kind} but got {Current.Kind} ('{Current.Text}')");
            return Current; // synthetic
        }
        return Advance();
    }

    private bool Match(TokenKind kind) { if (Current.Kind == kind) { Advance(); return true; } return false; }

    private bool MatchKeyword(string lower)
        => Current.Kind != TokenKind.EndOfFile &&
           string.Equals(Current.Value, lower, StringComparison.OrdinalIgnoreCase)
           && (Advance() != null);

    private bool PeekKeyword(string lower, int offset = 0)
        => (_pos + offset) < _tokens.Count &&
           string.Equals(_tokens[_pos + offset].Value, lower, StringComparison.OrdinalIgnoreCase);

    private void SkipNewLines()
    {
        while (Current.Kind is TokenKind.NewLine or TokenKind.Comment)
            Advance();
    }

    private void SkipToEndOfLine()
    {
        while (!AtEnd() && Current.Kind != TokenKind.NewLine)
            Advance();
    }

    private void ConsumeEndOfLine()
    {
        // skip inline comments first
        while (Current.Kind == TokenKind.Comment) Advance();
        if (Current.Kind == TokenKind.NewLine || Current.Kind == TokenKind.Colon)
            Advance();
    }

    private void RecordError(string message)
    {
        Errors.Add(new ParseError(message, Current.Line, Current.Column));
        SkipToEndOfLine();
    }

    // ── access modifier helper ───────────────────────────────────────────────

    private AccessModifier ParseAccessModifier()
    {
        return Current.Kind switch
        {
            TokenKind.Public  => (Advance(), AccessModifier.Public ).Item2,
            TokenKind.Private => (Advance(), AccessModifier.Private).Item2,
            TokenKind.Friend  => (Advance(), AccessModifier.Friend ).Item2,
            TokenKind.Global  => (Advance(), AccessModifier.Global ).Item2,
            _ => AccessModifier.Default,
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // Top-level members
    // ════════════════════════════════════════════════════════════════════════

    private SyntaxNode? ParseTopLevelMember()
    {
        // Skip stray newlines
        if (Current.Kind is TokenKind.NewLine or TokenKind.Comment) { Advance(); return null; }

        var tok = Current;

        // Option statement
        if (Current.Kind == TokenKind.Option) return ParseOptionStatement();

        // Attribute statement
        if (Current.Kind == TokenKind.Attribute) return ParseAttributeStatement();

        // DefType statements
        if (Current.Kind is TokenKind.DefBool or TokenKind.DefByte or TokenKind.DefInt or
            TokenKind.DefLng  or TokenKind.DefSng  or TokenKind.DefDbl or
            TokenKind.DefCur  or TokenKind.DefDate or TokenKind.DefStr or
            TokenKind.DefObj  or TokenKind.DefVar)
        {
            SkipToEndOfLine(); ConsumeEndOfLine();
            return null; // not modelled in depth
        }

        // Access modifier may precede various declarations
        bool isStatic = Current.Kind == TokenKind.Static;
        if (isStatic) { /* will be consumed per-branch */ }

        var access = ParseAccessModifier();

        // Static keyword can appear after access modifier
        if (Current.Kind == TokenKind.Static && !isStatic) { isStatic = true; Advance(); }

        // Sub / Function / Property
        if (Current.Kind is TokenKind.Sub or TokenKind.Function or TokenKind.Property)
            return ParseProcedureDeclaration(access, isStatic, tok);

        // Declare (external DLL declaration)
        if (Current.Kind == TokenKind.Declare) return ParseExternalDeclaration(access, tok);

        // Type / Enum
        if (Current.Kind == TokenKind.Type) return ParseTypeDeclaration(access, tok);
        if (Current.Kind == TokenKind.Enum) return ParseEnumDeclaration(access, tok);

        // Variable / Const declarations at module scope
        if (Current.Kind is TokenKind.Dim or TokenKind.Const)
            return ParseVariableDeclaration(access, isStatic, tok);

        // If we consumed an access modifier but nothing matched, fall through to statement
        return ParseStatement();
    }

    // ── Option ───────────────────────────────────────────────────────────────

    private OptionStatementSyntax ParseOptionStatement()
    {
        var tok = Advance(); // consume "Option"
        var node = new OptionStatementSyntax(tok);

        if (Current.Kind == TokenKind.Explicit) { node.OptionType = "Explicit"; Advance(); }
        else if (Current.Kind == TokenKind.Base)
        {
            node.OptionType = "Base";
            Advance();
            if (Current.Kind is TokenKind.IntegerLiteral or TokenKind.LongLiteral)
            { node.OptionValue = Current.Text; Advance(); }
        }
        else if (Current.Kind == TokenKind.Compare)
        {
            node.OptionType = "Compare";
            Advance();
            if (Current.Kind is TokenKind.Text or TokenKind.Binary)
            { node.OptionValue = Current.Value; Advance(); }
        }
        else if (Current.Kind == TokenKind.Private_Keyword) { node.OptionType = "Private"; Advance(); }
        else { node.OptionType = Current.Text; Advance(); }

        ConsumeEndOfLine();
        return node;
    }

    // ── Attribute ─────────────────────────────────────────────────────────────

    private AttributeStatementSyntax ParseAttributeStatement()
    {
        var tok  = Advance(); // consume "Attribute"
        var node = new AttributeStatementSyntax(tok);

        // Attribute VB_Name = "..."
        var nameParts = new System.Text.StringBuilder();
        while (Current.Kind is TokenKind.Identifier or TokenKind.Dot or TokenKind.Equals
                                or TokenKind.StringLiteral or TokenKind.IntegerLiteral
                                or TokenKind.BooleanLiteral)
        {
            nameParts.Append(Current.Text);
            Advance();
        }
        node.Name  = "Attribute";
        node.Value = nameParts.ToString();

        ConsumeEndOfLine();
        return node;
    }

    // ── Procedure ─────────────────────────────────────────────────────────────

    private ProcedureDeclarationSyntax ParseProcedureDeclaration(AccessModifier access, bool isStatic, Token startTok)
    {
        var kind = Current.Kind switch
        {
            TokenKind.Sub      => ProcedureKind.Sub,
            TokenKind.Function => ProcedureKind.Function,
            _                  => ProcedureKind.PropertyGet // resolved below
        };
        var tok = Advance();

        if (kind == ProcedureKind.PropertyGet)
        {
            kind = Current.Kind switch
            {
                TokenKind.Get => ProcedureKind.PropertyGet,
                TokenKind.Let => ProcedureKind.PropertyLet,
                TokenKind.Set => ProcedureKind.PropertySet,
                _ => ProcedureKind.PropertyGet
            };
            Advance();
        }

        var node = new ProcedureDeclarationSyntax(tok)
        {
            Access   = access,
            Kind     = kind,
            IsStatic = isStatic,
        };

        node.Name = Current.Kind == TokenKind.Identifier ? Current.Value : Current.Text.ToLowerInvariant();
        Advance();

        // Parameters
        if (Current.Kind == TokenKind.LeftParen)
        {
            Advance();
            while (Current.Kind != TokenKind.RightParen && !AtEnd() && Current.Kind != TokenKind.NewLine)
            {
                node.Parameters.Add(ParseParameter());
                if (Current.Kind == TokenKind.Comma) Advance();
            }
            if (Current.Kind == TokenKind.RightParen) Advance();
        }

        // Return type: As Type
        if (Current.Kind == TokenKind.As)
        {
            Advance();
            node.ReturnType = ParseTypeName();
        }

        ConsumeEndOfLine();

        // Body — IsBlockTerminator() handles "End Sub/Function/Property" automatically
        node.Body.AddRange(ParseBlock());

        // Consume End Sub / End Function / End Property
        ConsumeEndKeyword(kind);

        return node;
    }

    private void ConsumeEndKeyword(ProcedureKind kind)
    {
        if (Current.Kind == TokenKind.End)
        {
            Advance(); // End
            if (Current.Kind is TokenKind.Sub or TokenKind.Function or TokenKind.Property) Advance();
        }
        ConsumeEndOfLine();
    }

    private bool IsBlockTerminator()
    {
        if (Current.Kind == TokenKind.End)
        {
            var next = LookAhead().Kind;
            return next is TokenKind.Sub or TokenKind.Function or TokenKind.Property
                       or TokenKind.If or TokenKind.Select or TokenKind.With
                       or TokenKind.Type or TokenKind.Enum;
        }
        if (Current.Kind is TokenKind.Else or TokenKind.ElseIf or
            TokenKind.Case or TokenKind.Wend or TokenKind.Next or
            TokenKind.Loop) return true;
        return AtEnd();
    }

    // ── External declaration ──────────────────────────────────────────────────

    private ExternalDeclarationSyntax ParseExternalDeclaration(AccessModifier access, Token startTok)
    {
        var tok = Advance(); // consume "Declare"
        var node = new ExternalDeclarationSyntax(tok) { Access = access };

        node.IsFunction = Current.Kind == TokenKind.Function;
        if (Current.Kind is TokenKind.Sub or TokenKind.Function) Advance();

        node.Name = Current.Value; Advance();

        if (Current.Kind == TokenKind.Lib) { Advance(); node.LibName = Current.Value; Advance(); }
        if (Current.Kind == TokenKind.Alias) { Advance(); node.Alias = Current.Value; Advance(); }

        if (Current.Kind == TokenKind.LeftParen)
        {
            Advance();
            while (Current.Kind != TokenKind.RightParen && !AtEnd() && Current.Kind != TokenKind.NewLine)
            {
                node.Parameters.Add(ParseParameter());
                if (Current.Kind == TokenKind.Comma) Advance();
            }
            if (Current.Kind == TokenKind.RightParen) Advance();
        }

        if (Current.Kind == TokenKind.As) { Advance(); node.ReturnType = ParseTypeName(); }

        ConsumeEndOfLine();
        return node;
    }

    // ── Type declaration ──────────────────────────────────────────────────────

    private TypeDeclarationSyntax ParseTypeDeclaration(AccessModifier access, Token startTok)
    {
        var tok = Advance(); // "Type"
        var node = new TypeDeclarationSyntax(tok) { Access = access, Name = Current.Value };
        Advance();
        ConsumeEndOfLine();

        while (!AtEnd())
        {
            SkipNewLines();
            if (Current.Kind == TokenKind.End && LookAhead().Kind == TokenKind.Type) { Advance(); Advance(); break; }
            if (AtEnd()) break;

            var decl = ParseVariableDeclaration(AccessModifier.Default, false, Current);
            if (decl != null) node.Fields.Add((VariableDeclarationSyntax)decl);
        }
        ConsumeEndOfLine();
        return node;
    }

    // ── Enum declaration ──────────────────────────────────────────────────────

    private EnumDeclarationSyntax ParseEnumDeclaration(AccessModifier access, Token startTok)
    {
        var tok = Advance(); // "Enum"
        var node = new EnumDeclarationSyntax(tok) { Access = access, Name = Current.Value };
        Advance();
        ConsumeEndOfLine();

        while (!AtEnd())
        {
            SkipNewLines();
            if (Current.Kind == TokenKind.End && LookAhead().Kind == TokenKind.Enum) { Advance(); Advance(); break; }
            if (AtEnd()) break;
            if (Current.Kind == TokenKind.Identifier)
            {
                var memberTok = Current;
                var member = new EnumMemberSyntax(memberTok) { Name = Current.Value };
                Advance();
                if (Current.Kind == TokenKind.Equals) { Advance(); member.Value = ParseExpression(); }
                ConsumeEndOfLine();
                node.Members.Add(member);
            }
            else { SkipToEndOfLine(); ConsumeEndOfLine(); }
        }
        ConsumeEndOfLine();
        return node;
    }

    // ── Variable / Const declaration ─────────────────────────────────────────

    private SyntaxNode? ParseVariableDeclaration(AccessModifier access, bool isStatic, Token startTok)
    {
        bool isConst = Current.Kind == TokenKind.Const;
        if (Current.Kind is TokenKind.Dim or TokenKind.Const) Advance();

        var node = new VariableDeclarationSyntax(startTok)
        {
            Access   = access,
            IsConst  = isConst,
            IsStatic = isStatic,
        };

        do
        {
            if (Current.Kind == TokenKind.Comma) Advance();
            if (Current.Kind is TokenKind.NewLine or TokenKind.Comment or TokenKind.EndOfFile) break;

            var decTok = Current;
            var dec = new VariableDeclaratorSyntax(decTok);

            // WithEvents modifier
            if (Current.Kind == TokenKind.WithEvents) { dec.WithEvents = true; Advance(); }

            // Name
            dec.Name = Current.Kind == TokenKind.Identifier ? Current.Value : Current.Text.ToLowerInvariant();
            Advance();

            // Array bounds (skip for now)
            if (Current.Kind == TokenKind.LeftParen)
            {
                while (Current.Kind != TokenKind.RightParen && !AtEnd() && Current.Kind != TokenKind.NewLine)
                    Advance();
                if (Current.Kind == TokenKind.RightParen) Advance();
            }

            // As [New] Type
            if (Current.Kind == TokenKind.As)
            {
                Advance();
                if (Current.Kind == TokenKind.New) { dec.IsNew = true; Advance(); }
                dec.TypeName = ParseTypeName();
            }

            // Initialiser for Const
            if (Current.Kind == TokenKind.Equals)
            {
                Advance();
                dec.Initializer = ParseExpression();
            }

            node.Declarators.Add(dec);
        }
        while (Current.Kind == TokenKind.Comma);

        ConsumeEndOfLine();
        return node;
    }

    // ── Parameter ─────────────────────────────────────────────────────────────

    private ParameterSyntax ParseParameter()
    {
        var tok   = Current;
        var param = new ParameterSyntax(tok);

        if (Current.Kind == TokenKind.Optional) { param.Optional = true; Advance(); }
        if (Current.Kind == TokenKind.ByVal)    { param.ByVal    = true; Advance(); }
        else if (Current.Kind == TokenKind.ByRef) { param.ByRef  = true; Advance(); }
        if (Current.Kind == TokenKind.ParamArray) { param.ParamArray = true; Advance(); }

        param.Name = Current.Kind == TokenKind.Identifier ? Current.Value : Current.Text.ToLowerInvariant();
        Advance();

        // Array indicator
        if (Current.Kind == TokenKind.LeftParen)
        {
            while (Current.Kind != TokenKind.RightParen && !AtEnd() && Current.Kind != TokenKind.NewLine) Advance();
            if (Current.Kind == TokenKind.RightParen) Advance();
        }

        if (Current.Kind == TokenKind.As) { Advance(); param.TypeName = ParseTypeName(); }
        if (Current.Kind == TokenKind.Equals) { Advance(); param.DefaultValue = ParseExpression(); }

        return param;
    }

    // ── Type name ─────────────────────────────────────────────────────────────

    private string ParseTypeName()
    {
        var sb = new System.Text.StringBuilder();
        // Can be keyword-types or dotted names
        while (Current.Kind is TokenKind.Identifier or TokenKind.Dot or
                               TokenKind.Integer   or TokenKind.Long    or TokenKind.Single  or
                               TokenKind.Double    or TokenKind.String  or TokenKind.Boolean or
                               TokenKind.Date      or TokenKind.Variant or TokenKind.Object  or
                               TokenKind.Byte      or TokenKind.Currency or TokenKind.LongLong or
                               TokenKind.LongPtr)
        {
            sb.Append(Current.Text);
            Advance();
            if (Current.Kind == TokenKind.Dot) { sb.Append('.'); Advance(); }
            else break;
        }
        return sb.Length > 0 ? sb.ToString() : "Variant";
    }

    // ════════════════════════════════════════════════════════════════════════
    // Statement block
    // ════════════════════════════════════════════════════════════════════════

    private List<SyntaxNode> ParseBlock(Func<Token, bool>? additionalTerminator = null)
    {
        var stmts = new List<SyntaxNode>();
        while (!AtEnd() && !IsBlockTerminator())
        {
            if (additionalTerminator != null && additionalTerminator(Current)) break;
            SkipNewLines();
            if (AtEnd() || IsBlockTerminator()) break;
            if (additionalTerminator != null && additionalTerminator(Current)) break;

            var stmt = ParseStatement();
            if (stmt != null) stmts.Add(stmt);
        }
        return stmts;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Statement dispatch
    // ════════════════════════════════════════════════════════════════════════

    private SyntaxNode? ParseStatement()
    {
        // Skip inline comments
        while (Current.Kind == TokenKind.Comment) { Advance(); return null; }
        if (Current.Kind is TokenKind.NewLine or TokenKind.Colon) { Advance(); return null; }

        var tok = Current;

        return tok.Kind switch
        {
            TokenKind.On         => ParseOnErrorStatement(),
            TokenKind.GoTo       => ParseGoToStatement(false),
            TokenKind.GoSub      => ParseGoToStatement(true),
            TokenKind.Return     => ParseReturnStatement(),
            TokenKind.Resume     => ParseResumeStatement(),
            TokenKind.If         => ParseIfStatement(),
            TokenKind.Select     => ParseSelectStatement(),
            TokenKind.For        => ParseForStatement(),
            TokenKind.While      => ParseWhileStatement(),
            TokenKind.Do         => ParseDoStatement(),
            TokenKind.With       => ParseWithStatement(),
            TokenKind.Exit       => ParseExitStatement(),
            TokenKind.Stop       => ParseStopStatement(),
            TokenKind.RaiseEvent => ParseRaiseEventStatement(),
            TokenKind.Error      => ParseErrorStatement(),
            TokenKind.Dim        => ParseVariableDeclaration(AccessModifier.Default, false, tok),
            TokenKind.Static     => ParseVariableDeclaration(AccessModifier.Default, true, tok),
            TokenKind.Const      => ParseVariableDeclaration(AccessModifier.Default, false, tok),
            TokenKind.Set        => ParseAssignmentStatement(isSet: true),
            TokenKind.Let        => ParseAssignmentStatement(isSet: false, isLet: true),
            TokenKind.Call       => ParseCallStatement(),
            TokenKind.End        => ParseEndStatement(),
            // Label: identifier followed immediately by ':'
            _ when IsLabel()     => ParseLabelStatement(),
            _                    => ParseExpressionStatement(),
        };
    }

    private bool IsLabel()
        => Current.Kind == TokenKind.Identifier && LookAhead().Kind == TokenKind.Colon;

    // ── On Error ──────────────────────────────────────────────────────────────

    private OnErrorStatementSyntax ParseOnErrorStatement()
    {
        var tok = Advance(); // "On"
        Consume(TokenKind.Error);
        var node = new OnErrorStatementSyntax(tok);

        if (Current.Kind == TokenKind.Resume)
        {
            Advance();
            Consume(TokenKind.Next);
            node.Kind = OnErrorKind.ResumeNext;
        }
        else if (Current.Kind == TokenKind.GoTo)
        {
            Advance();
            if (Current.Kind == TokenKind.IntegerLiteral && Current.Text == "0")
            {
                Advance();
                node.Kind = OnErrorKind.GoTo0;
            }
            else
            {
                node.Kind       = OnErrorKind.GoToLabel;
                node.GotoLabel  = Current.Kind == TokenKind.Identifier ? Current.Value : Current.Text;
                Advance();
            }
        }

        ConsumeEndOfLine();
        return node;
    }

    // ── GoTo / GoSub ─────────────────────────────────────────────────────────

    private GoToStatementSyntax ParseGoToStatement(bool isGoSub)
    {
        var tok = Advance(); // GoTo or GoSub
        var node = new GoToStatementSyntax(tok) { IsGoSub = isGoSub };
        node.Label = Current.Kind == TokenKind.Identifier
            ? Current.Value
            : Current.Text;
        Advance();
        ConsumeEndOfLine();
        return node;
    }

    // ── Return ───────────────────────────────────────────────────────────────

    private ReturnStatementSyntax ParseReturnStatement()
    {
        var tok = Advance(); // Return
        ConsumeEndOfLine();
        return new ReturnStatementSyntax(tok);
    }

    // ── Resume ───────────────────────────────────────────────────────────────

    private ResumeStatementSyntax ParseResumeStatement()
    {
        var tok  = Advance(); // Resume
        var node = new ResumeStatementSyntax(tok);
        if (Current.Kind == TokenKind.Next)  { node.IsNext = true; Advance(); }
        else if (Current.Kind == TokenKind.Identifier) { node.Label = Current.Value; Advance(); }
        ConsumeEndOfLine();
        return node;
    }

    // ── If ────────────────────────────────────────────────────────────────────

    private IfStatementSyntax ParseIfStatement()
    {
        var tok  = Advance(); // "If"
        var node = new IfStatementSyntax(tok);
        node.Condition = ParseExpression();
        Match(TokenKind.Then);

        // Single-line If: "If cond Then stmt [Else stmt]"
        if (Current.Kind != TokenKind.NewLine && Current.Kind != TokenKind.Comment && !AtEnd())
        {
            node.IsSingleLine = true;
            var thenStmt = ParseStatement();
            if (thenStmt != null) node.ThenBody.Add(thenStmt);
            if (Current.Kind == TokenKind.Else)
            {
                Advance();
                node.ElseBody = new List<SyntaxNode>();
                var elseStmt = ParseStatement();
                if (elseStmt != null) node.ElseBody.Add(elseStmt);
            }
            ConsumeEndOfLine();
            return node;
        }

        // Block If
        ConsumeEndOfLine();
        node.ThenBody.AddRange(ParseBlock());

        while (Current.Kind == TokenKind.ElseIf)
        {
            var eifTok = Advance();
            var clause = new IfClauseSyntax(eifTok);
            clause.Condition = ParseExpression();
            Match(TokenKind.Then);
            ConsumeEndOfLine();
            clause.Body.AddRange(ParseBlock());
            node.ElseIfs.Add(clause);
        }

        if (Current.Kind == TokenKind.Else)
        {
            Advance();
            ConsumeEndOfLine();
            node.ElseBody = ParseBlock();
        }

        // End If
        if (Current.Kind == TokenKind.End) { Advance(); Match(TokenKind.If); }
        else if (Current.Kind == TokenKind.EndIf) Advance();

        ConsumeEndOfLine();
        return node;
    }

    // ── Select Case ───────────────────────────────────────────────────────────

    private SelectStatementSyntax ParseSelectStatement()
    {
        var tok = Advance(); // "Select"
        Match(TokenKind.Case);
        var node = new SelectStatementSyntax(tok);
        node.TestExpression = ParseExpression();
        ConsumeEndOfLine();
        SkipNewLines();

        while (!AtEnd() && !(Current.Kind == TokenKind.End && LookAhead().Kind == TokenKind.Select))
        {
            if (Current.Kind != TokenKind.Case) { SkipToEndOfLine(); ConsumeEndOfLine(); continue; }

            var caseTok = Advance(); // "Case"
            var clause  = new CaseClauseSyntax(caseTok);

            if (Current.Kind == TokenKind.Else) { clause.IsElse = true; Advance(); }
            else
            {
                clause.Patterns.Add(ParseCasePattern());
                while (Current.Kind == TokenKind.Comma) { Advance(); clause.Patterns.Add(ParseCasePattern()); }
            }
            ConsumeEndOfLine();
            clause.Body.AddRange(ParseBlock());
            node.Cases.Add(clause);
            SkipNewLines();
        }

        if (Current.Kind == TokenKind.End) { Advance(); Match(TokenKind.Select); }
        else if (Current.Kind == TokenKind.EndSelect) Advance();
        ConsumeEndOfLine();
        return node;
    }

    private ExpressionSyntax ParseCasePattern()
    {
        // Simplified: parse an expression (handles "Is > 5", "1 To 10", plain value)
        if (Current.Kind == TokenKind.Is)
        {
            var opTok = Advance();
            var op = Current.Text; Advance();
            var expr = ParseExpression();
            return new BinaryExpressionSyntax(opTok) { Left = new IdentifierExpressionSyntax(opTok) { Name = "Is" }, Operator = op, Right = expr };
        }
        var left = ParseExpression();
        if (Current.Kind == TokenKind.To)
        {
            var toTok = Advance();
            var right = ParseExpression();
            return new BinaryExpressionSyntax(toTok) { Left = left, Operator = "To", Right = right };
        }
        return left;
    }

    // ── For ───────────────────────────────────────────────────────────────────

    private SyntaxNode ParseForStatement()
    {
        var tok = Advance(); // "For"

        if (Current.Kind == TokenKind.Each)
        {
            Advance();
            var node = new ForEachStatementSyntax(tok) { Iterator = Current.Value };
            Advance();
            Match(TokenKind.In);
            node.Collection = ParseExpression();
            ConsumeEndOfLine();
            node.Body.AddRange(ParseBlock());
            if (Current.Kind == TokenKind.Next) Advance();
            if (Current.Kind == TokenKind.Identifier) Advance(); // optional counter name
            ConsumeEndOfLine();
            return node;
        }
        else
        {
            var node = new ForStatementSyntax(tok) { Counter = Current.Value };
            Advance();
            Match(TokenKind.Equals);
            node.Start = ParseExpression();
            Match(TokenKind.To);
            node.Limit = ParseExpression();
            if (Current.Kind == TokenKind.Step) { Advance(); node.Step = ParseExpression(); }
            ConsumeEndOfLine();
            node.Body.AddRange(ParseBlock());
            if (Current.Kind == TokenKind.Next) Advance();
            if (Current.Kind == TokenKind.Identifier) Advance();
            ConsumeEndOfLine();
            return node;
        }
    }

    // ── While / Wend ─────────────────────────────────────────────────────────

    private WhileStatementSyntax ParseWhileStatement()
    {
        var tok  = Advance(); // "While"
        var node = new WhileStatementSyntax(tok);
        node.Condition = ParseExpression();
        ConsumeEndOfLine();
        node.Body.AddRange(ParseBlock());
        if (Current.Kind == TokenKind.Wend) Advance();
        ConsumeEndOfLine();
        return node;
    }

    // ── Do / Loop ─────────────────────────────────────────────────────────────

    private DoStatementSyntax ParseDoStatement()
    {
        var tok  = Advance(); // "Do"
        var node = new DoStatementSyntax(tok);

        if (Current.Kind is TokenKind.While or TokenKind.Until)
        {
            node.ConditionPlacement = DoLoopConditionPlacement.AtTop;
            node.IsUntil = Current.Kind == TokenKind.Until;
            Advance();
            node.Condition = ParseExpression();
        }

        ConsumeEndOfLine();
        node.Body.AddRange(ParseBlock());

        if (Current.Kind == TokenKind.Loop)
        {
            Advance();
            if (Current.Kind is TokenKind.While or TokenKind.Until)
            {
                node.ConditionPlacement = DoLoopConditionPlacement.AtBottom;
                node.IsUntil = Current.Kind == TokenKind.Until;
                Advance();
                node.Condition = ParseExpression();
            }
        }
        ConsumeEndOfLine();
        return node;
    }

    // ── With ─────────────────────────────────────────────────────────────────

    private WithStatementSyntax ParseWithStatement()
    {
        var tok  = Advance(); // "With"
        var node = new WithStatementSyntax(tok);
        node.Object = ParseExpression();
        ConsumeEndOfLine();
        node.Body.AddRange(ParseBlock());
        if (Current.Kind == TokenKind.End) { Advance(); Match(TokenKind.With); }
        else if (Current.Kind == TokenKind.EndWith) Advance();
        ConsumeEndOfLine();
        return node;
    }

    // ── Exit ──────────────────────────────────────────────────────────────────

    private ExitStatementSyntax ParseExitStatement()
    {
        var tok = Advance(); // "Exit"
        var node = new ExitStatementSyntax(tok);
        node.Target = Current.Kind switch
        {
            TokenKind.Sub      => (Advance(), "Sub").Item2,
            TokenKind.Function => (Advance(), "Function").Item2,
            TokenKind.For      => (Advance(), "For").Item2,
            TokenKind.Do       => (Advance(), "Do").Item2,
            TokenKind.Property => (Advance(), "Property").Item2,
            _                  => string.Empty,
        };
        ConsumeEndOfLine();
        return node;
    }

    // ── Stop ─────────────────────────────────────────────────────────────────

    private StopStatementSyntax ParseStopStatement()
    {
        var tok = Advance();
        ConsumeEndOfLine();
        return new StopStatementSyntax(tok);
    }

    // ── RaiseEvent ───────────────────────────────────────────────────────────

    private RaiseEventStatementSyntax ParseRaiseEventStatement()
    {
        var tok = Advance(); // "RaiseEvent"
        var node = new RaiseEventStatementSyntax(tok) { EventName = Current.Value };
        Advance();
        if (Current.Kind == TokenKind.LeftParen)
        {
            Advance();
            ParseArgumentList(node.Arguments);
            if (Current.Kind == TokenKind.RightParen) Advance();
        }
        ConsumeEndOfLine();
        return node;
    }

    // ── Error ─────────────────────────────────────────────────────────────────

    private ErrorStatementSyntax ParseErrorStatement()
    {
        var tok = Advance(); // "Error"
        var node = new ErrorStatementSyntax(tok) { ErrorNumber = ParseExpression() };
        ConsumeEndOfLine();
        return node;
    }

    // ── Label ─────────────────────────────────────────────────────────────────

    private LabelStatementSyntax ParseLabelStatement()
    {
        var tok  = Current;
        var node = new LabelStatementSyntax(tok) { Name = Current.Value };
        Advance(); // identifier
        Advance(); // colon
        ConsumeEndOfLine();
        return node;
    }

    // ── Set / Let assignment ──────────────────────────────────────────────────

    private AssignmentStatementSyntax ParseAssignmentStatement(bool isSet = false, bool isLet = false)
    {
        var tok = Advance(); // Set or Let
        var node = new AssignmentStatementSyntax(tok) { IsSet = isSet, IsLet = isLet };
        node.Target = ParsePrimaryExpression();
        Match(TokenKind.Equals);
        node.Value = ParseExpression();
        ConsumeEndOfLine();
        return node;
    }

    // ── Call ──────────────────────────────────────────────────────────────────

    private CallStatementSyntax ParseCallStatement()
    {
        var tok = Advance(); // Call
        var node = new CallStatementSyntax(tok) { IsCallKeyword = true };
        node.Target = ParsePrimaryExpression();
        if (Current.Kind == TokenKind.LeftParen)
        {
            Advance();
            ParseArgumentList(node.Arguments);
            if (Current.Kind == TokenKind.RightParen) Advance();
        }
        ConsumeEndOfLine();
        return node;
    }

    // ── End statement (bare End) ──────────────────────────────────────────────

    private SyntaxNode? ParseEndStatement()
    {
        var tok = Current;
        // "End" followed by Sub/Function etc. is handled by block terminators
        var next = LookAhead().Kind;
        if (next is TokenKind.Sub or TokenKind.Function or TokenKind.Property
                 or TokenKind.If or TokenKind.Select or TokenKind.With
                 or TokenKind.Type or TokenKind.Enum) return null; // let block parser handle it

        Advance(); // bare End (program termination)
        ConsumeEndOfLine();
        return new StopStatementSyntax(tok); // treat like Stop
    }

    // ── Expression statement (assignment or call without keyword) ─────────────

    private SyntaxNode? ParseExpressionStatement()
    {
        var tok  = Current;
        var expr = ParseExpression();

        if (Current.Kind == TokenKind.Equals)
        {
            // Assignment: LHS = RHS
            Advance();
            var rhs  = ParseExpression();
            var node = new AssignmentStatementSyntax(tok)
            {
                Target = expr,
                Value  = rhs,
            };
            ConsumeEndOfLine();
            return node;
        }
        else
        {
            // Bare call: procedure name [arg1, arg2, ...]
            var node = new CallStatementSyntax(tok) { Target = expr };
            while (!AtEnd() && Current.Kind != TokenKind.NewLine && Current.Kind != TokenKind.Comment)
            {
                if (Current.Kind == TokenKind.Comma) Advance();
                if (Current.Kind is TokenKind.NewLine or TokenKind.Comment or TokenKind.EndOfFile) break;
                node.Arguments.Add(ParseExpression());
            }
            ConsumeEndOfLine();
            return node;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Expression parsing  (Pratt / precedence climbing)
    // ════════════════════════════════════════════════════════════════════════

    private ExpressionSyntax ParseExpression() => ParseOr();

    private ExpressionSyntax ParseOr()
    {
        var left = ParseAnd();
        while (Current.Kind is TokenKind.Or or TokenKind.Xor or TokenKind.Eqv or TokenKind.Imp)
        {
            var op = Current.Text; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseAnd() };
        }
        return left;
    }

    private ExpressionSyntax ParseAnd()
    {
        var left = ParseNot();
        while (Current.Kind == TokenKind.And)
        {
            var op = Current.Text; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseNot() };
        }
        return left;
    }

    private ExpressionSyntax ParseNot()
    {
        if (Current.Kind == TokenKind.Not)
        {
            var tok = Advance();
            return new UnaryExpressionSyntax(tok) { Operator = "Not", Operand = ParseNot() };
        }
        return ParseComparison();
    }

    private ExpressionSyntax ParseComparison()
    {
        var left = ParseConcat();
        while (Current.Kind is TokenKind.Equals or TokenKind.NotEquals or
                               TokenKind.LessThan or TokenKind.GreaterThan or
                               TokenKind.LessOrEqual or TokenKind.GreaterOrEqual or
                               TokenKind.Is or TokenKind.Like)
        {
            var op = Current.Text; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseConcat() };
        }
        return left;
    }

    private ExpressionSyntax ParseConcat()
    {
        var left = ParseAddSub();
        while (Current.Kind == TokenKind.Ampersand)
        {
            var op = "&"; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseAddSub() };
        }
        return left;
    }

    private ExpressionSyntax ParseAddSub()
    {
        var left = ParseMulDiv();
        while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var op = Current.Text; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseMulDiv() };
        }
        return left;
    }

    private ExpressionSyntax ParseMulDiv()
    {
        var left = ParseIntDiv();
        while (Current.Kind is TokenKind.Star or TokenKind.Slash)
        {
            var op = Current.Text; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseIntDiv() };
        }
        return left;
    }

    private ExpressionSyntax ParseIntDiv()
    {
        var left = ParseMod();
        while (Current.Kind == TokenKind.BackSlash)
        {
            var op = "\\"; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseMod() };
        }
        return left;
    }

    private ExpressionSyntax ParseMod()
    {
        var left = ParseUnary();
        while (Current.Kind == TokenKind.Mod)
        {
            var op = "Mod"; var tok = Advance();
            left = new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseUnary() };
        }
        return left;
    }

    private ExpressionSyntax ParseUnary()
    {
        if (Current.Kind == TokenKind.Minus)
        {
            var tok = Advance();
            return new UnaryExpressionSyntax(tok) { Operator = "-", Operand = ParsePower() };
        }
        if (Current.Kind == TokenKind.Plus)
        {
            var tok = Advance();
            return new UnaryExpressionSyntax(tok) { Operator = "+", Operand = ParsePower() };
        }
        return ParsePower();
    }

    private ExpressionSyntax ParsePower()
    {
        var left = ParseCallOrIndex();
        if (Current.Kind == TokenKind.Caret)
        {
            var op = "^"; var tok = Advance();
            return new BinaryExpressionSyntax(tok) { Left = left, Operator = op, Right = ParseUnary() };
        }
        return left;
    }

    private ExpressionSyntax ParseCallOrIndex()
    {
        var expr = ParsePrimaryExpression();

        while (true)
        {
            if (Current.Kind == TokenKind.Dot || Current.Kind == TokenKind.Bang)
            {
                var tok = Advance();
                var member = Current.Kind == TokenKind.Identifier ? Current.Value : Current.Text;
                var memberTok = Current;
                Advance();
                expr = new MemberAccessExpressionSyntax(tok) { Object = expr, Member = member };
            }
            else if (Current.Kind == TokenKind.LeftParen)
            {
                var tok = Advance();
                var args = new List<ExpressionSyntax>();
                ParseArgumentList(args);
                if (Current.Kind == TokenKind.RightParen) Advance();
                expr = new InvocationExpressionSyntax(tok) { Target = expr, Arguments = { } };
                foreach (var a in args) ((InvocationExpressionSyntax)expr).Arguments.Add(a);
            }
            else break;
        }
        return expr;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        var tok = Current;

        // Literals
        if (tok.Kind is TokenKind.IntegerLiteral or TokenKind.LongLiteral or
                        TokenKind.DoubleLiteral   or TokenKind.SingleLiteral or
                        TokenKind.CurrencyLiteral or TokenKind.StringLiteral or
                        TokenKind.DateLiteral     or TokenKind.BooleanLiteral or
                        TokenKind.NothingLiteral  or TokenKind.NullLiteral or
                        TokenKind.EmptyLiteral)
        {
            Advance();
            return new LiteralExpressionSyntax(tok);
        }

        // Me
        if (tok.Kind == TokenKind.Me) { Advance(); return new IdentifierExpressionSyntax(tok) { Name = "Me" }; }

        // New TypeName
        if (tok.Kind == TokenKind.New)
        {
            Advance();
            var typeName = ParseTypeName();
            return new NewObjectExpressionSyntax(tok) { TypeName = typeName };
        }

        // TypeOf expr Is Type
        if (tok.Kind == TokenKind.TypeOf)
        {
            Advance();
            var obj = ParseExpression();
            Match(TokenKind.Is);
            var typeName = ParseTypeName();
            return new TypeOfExpressionSyntax(tok) { Object = obj, TypeName = typeName };
        }

        // AddressOf
        if (tok.Kind == TokenKind.AddressOf)
        {
            Advance();
            var inner = ParsePrimaryExpression();
            return new UnaryExpressionSyntax(tok) { Operator = "AddressOf", Operand = inner };
        }

        // Parenthesised
        if (tok.Kind == TokenKind.LeftParen)
        {
            Advance();
            var inner = ParseExpression();
            if (Current.Kind == TokenKind.RightParen) Advance();
            return new ParenthesisedExpressionSyntax(tok) { Inner = inner };
        }

        // Identifier (or keyword used as name)
        if (tok.Kind == TokenKind.Identifier || IsKeywordUsableAsName(tok.Kind))
        {
            Advance();
            return new IdentifierExpressionSyntax(tok) { Name = tok.Value };
        }

        // Unknown — emit a placeholder and consume so we don't loop
        Advance();
        return new IdentifierExpressionSyntax(tok) { Name = tok.Text };
    }

    private static bool IsKeywordUsableAsName(TokenKind k) => k is
        TokenKind.String or TokenKind.Date or TokenKind.Error;

    // ── Argument list ────────────────────────────────────────────────────────

    private void ParseArgumentList(List<ExpressionSyntax> args)
    {
        while (!AtEnd() && Current.Kind != TokenKind.RightParen &&
               Current.Kind != TokenKind.NewLine)
        {
            if (Current.Kind == TokenKind.Comma) { args.Add(new LiteralExpressionSyntax(Current)); Advance(); continue; }
            args.Add(ParseExpression());
            if (Current.Kind == TokenKind.Comma) Advance(); else break;
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// ParseError
// ════════════════════════════════════════════════════════════════════════════

public sealed class ParseError
{
    public string Message { get; }
    public int    Line    { get; }
    public int    Column  { get; }

    public ParseError(string message, int line, int column)
    {
        Message = message;
        Line    = line;
        Column  = column;
    }

    public override string ToString() => $"Parse error at L{Line}:C{Column}: {Message}";
}
