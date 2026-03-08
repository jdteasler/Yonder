using System.Text;

namespace Vb6Lint.Core.Lexer;

/// <summary>
/// Transforms raw VB6 source text into a flat list of <see cref="Token"/>s.
///
/// Design notes:
/// • VB6 is case-insensitive; all keywords are matched case-insensitively and
///   stored in their original casing, but the <see cref="Token.Value"/> field
///   holds the lower-case normalised form.
/// • Physical line continuations (_ at end of line preceded only by whitespace)
///   are consumed transparently — the token stream never contains them.
/// • Statement separators within a physical line (bare ':') produce a
///   <see cref="TokenKind.Colon"/> token so the parser can treat them like a
///   NewLine for statement boundaries.
/// • Comments are emitted as <see cref="TokenKind.Comment"/> tokens so that
///   analysers that inspect comment text can do so, but the parser skips them.
/// </summary>
public sealed class Vb6Lexer
{
    private readonly string _source;
    private int    _pos;
    private int    _line;
    private int    _col;

    // Keyword lookup table (lower-case → TokenKind)
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dim"]        = TokenKind.Dim,
        ["public"]     = TokenKind.Public,
        ["private"]    = TokenKind.Private,
        ["friend"]     = TokenKind.Friend,
        ["global"]     = TokenKind.Global,
        ["static"]     = TokenKind.Static,
        ["const"]      = TokenKind.Const,
        ["type"]       = TokenKind.Type,
        ["enum"]       = TokenKind.Enum,
        ["sub"]        = TokenKind.Sub,
        ["function"]   = TokenKind.Function,
        ["property"]   = TokenKind.Property,
        ["get"]        = TokenKind.Get,
        ["set"]        = TokenKind.Set,
        ["let"]        = TokenKind.Let,
        ["end"]        = TokenKind.End,
        ["exit"]       = TokenKind.Exit,
        ["return"]     = TokenKind.Return,
        ["class"]      = TokenKind.Class,
        ["implements"] = TokenKind.Implements,
        ["event"]      = TokenKind.Event,
        ["raiseevent"] = TokenKind.RaiseEvent,
        ["withevents"] = TokenKind.WithEvents,
        ["if"]         = TokenKind.If,
        ["then"]       = TokenKind.Then,
        ["else"]       = TokenKind.Else,
        ["elseif"]     = TokenKind.ElseIf,
        ["select"]     = TokenKind.Select,
        ["case"]       = TokenKind.Case,
        ["for"]        = TokenKind.For,
        ["each"]       = TokenKind.Each,
        ["in"]         = TokenKind.In,
        ["to"]         = TokenKind.To,
        ["step"]       = TokenKind.Step,
        ["next"]       = TokenKind.Next,
        ["while"]      = TokenKind.While,
        ["wend"]       = TokenKind.Wend,
        ["do"]         = TokenKind.Do,
        ["loop"]       = TokenKind.Loop,
        ["until"]      = TokenKind.Until,
        ["goto"]       = TokenKind.GoTo,
        ["gosub"]      = TokenKind.GoSub,
        ["resume"]     = TokenKind.Resume,
        ["on"]         = TokenKind.On,
        ["error"]      = TokenKind.Error,
        ["with"]       = TokenKind.With,
        ["option"]     = TokenKind.Option,
        ["explicit"]   = TokenKind.Explicit,
        ["base"]       = TokenKind.Base,
        ["compare"]    = TokenKind.Compare,
        ["text"]       = TokenKind.Text,
        ["binary"]     = TokenKind.Binary,
        ["byval"]      = TokenKind.ByVal,
        ["byref"]      = TokenKind.ByRef,
        ["optional"]   = TokenKind.Optional,
        ["paramarray"] = TokenKind.ParamArray,
        ["as"]         = TokenKind.As,
        ["new"]        = TokenKind.New,
        ["is"]         = TokenKind.Is,
        ["like"]       = TokenKind.Like,
        ["typeof"]     = TokenKind.TypeOf,
        ["integer"]    = TokenKind.Integer,
        ["long"]       = TokenKind.Long,
        ["single"]     = TokenKind.Single,
        ["double"]     = TokenKind.Double,
        ["string"]     = TokenKind.String,
        ["boolean"]    = TokenKind.Boolean,
        ["date"]       = TokenKind.Date,
        ["variant"]    = TokenKind.Variant,
        ["object"]     = TokenKind.Object,
        ["byte"]       = TokenKind.Byte,
        ["currency"]   = TokenKind.Currency,
        ["and"]        = TokenKind.And,
        ["or"]         = TokenKind.Or,
        ["not"]        = TokenKind.Not,
        ["xor"]        = TokenKind.Xor,
        ["eqv"]        = TokenKind.Eqv,
        ["imp"]        = TokenKind.Imp,
        ["mod"]        = TokenKind.Mod,
        ["call"]       = TokenKind.Call,
        ["declare"]    = TokenKind.Declare,
        ["lib"]        = TokenKind.Lib,
        ["alias"]      = TokenKind.Alias,
        ["addressof"]  = TokenKind.AddressOf,
        ["me"]         = TokenKind.Me,
        ["stop"]       = TokenKind.Stop,
        ["rem"]        = TokenKind.Rem,
        ["true"]       = TokenKind.BooleanLiteral,
        ["false"]      = TokenKind.BooleanLiteral,
        ["nothing"]    = TokenKind.NothingLiteral,
        ["null"]       = TokenKind.NullLiteral,
        ["empty"]      = TokenKind.EmptyLiteral,
        ["attribute"]  = TokenKind.Attribute,
        ["defbool"]    = TokenKind.DefBool,
        ["defbyte"]    = TokenKind.DefByte,
        ["defint"]     = TokenKind.DefInt,
        ["deflng"]     = TokenKind.DefLng,
        ["defsng"]     = TokenKind.DefSng,
        ["defdbl"]     = TokenKind.DefDbl,
        ["defcur"]     = TokenKind.DefCur,
        ["defdate"]    = TokenKind.DefDate,
        ["defstr"]     = TokenKind.DefStr,
        ["defobj"]     = TokenKind.DefObj,
        ["defvar"]     = TokenKind.DefVar,
    };

    public Vb6Lexer(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _pos  = 0;
        _line = 1;
        _col  = 1;
    }

    /// <summary>Tokenises the entire source and returns the token list.</summary>
    public IReadOnlyList<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var tok = NextToken();
            tokens.Add(tok);
            if (tok.Kind == TokenKind.EndOfFile) break;
        }
        return tokens;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private char Current  => _pos < _source.Length ? _source[_pos]     : '\0';
    private char LookAhead(int offset = 1) => (_pos + offset) < _source.Length ? _source[_pos + offset] : '\0';

    private char Advance()
    {
        char c = _source[_pos++];
        if (c == '\n') { _line++; _col = 1; }
        else            { _col++; }
        return c;
    }

    private Token MakeToken(TokenKind kind, string text, string? value = null, int? line = null, int? col = null, int? pos = null)
        => new(kind, text, value ?? text.ToLowerInvariant(), line ?? _line, col ?? _col, pos ?? _pos);

    // ── main dispatcher ──────────────────────────────────────────────────────

    private Token NextToken()
    {
        // Skip horizontal whitespace and line continuations (' _' + newline)
        while (_pos < _source.Length)
        {
            char ch = _source[_pos];
            if (ch == ' ' || ch == '\t') { Advance(); continue; }

            // Line continuation: underscore preceded by a space, followed by optional
            // whitespace and a newline.  We peek ahead to confirm before consuming.
            if (ch == '_')
            {
                int peek = _pos + 1;
                while (peek < _source.Length && (_source[peek] == ' ' || _source[peek] == '\t'))
                    peek++;
                if (peek < _source.Length && (_source[peek] == '\r' || _source[peek] == '\n'))
                {
                    Advance(); // underscore
                    while (_pos < _source.Length && (_source[_pos] == ' ' || _source[_pos] == '\t'))
                        Advance();
                    if (_pos < _source.Length && _source[_pos] == '\r') Advance();
                    if (_pos < _source.Length && _source[_pos] == '\n') Advance();
                    continue;
                }
            }
            break;
        }

        if (_pos >= _source.Length)
            return MakeToken(TokenKind.EndOfFile, string.Empty);

        int startLine = _line;
        int startCol  = _col;
        int startPos  = _pos;

        char c = Current;

        // ── newline ──────────────────────────────────────────────────────────
        if (c == '\r' || c == '\n')
        {
            var sb = new StringBuilder();
            if (c == '\r') { sb.Append(Advance()); }
            if (Current == '\n') { sb.Append(Advance()); }
            return new Token(TokenKind.NewLine, sb.ToString(), "\n", startLine, startCol, startPos);
        }

        // ── comment: ' or Rem ─────────────────────────────────────────────
        if (c == '\'')
            return ReadLineComment(startLine, startCol, startPos);

        // ── string literal ────────────────────────────────────────────────
        if (c == '"')
            return ReadStringLiteral(startLine, startCol, startPos);

        // ── date literal #…# ─────────────────────────────────────────────
        if (c == '#')
            return ReadDateOrHash(startLine, startCol, startPos);

        // ── numeric literal ───────────────────────────────────────────────
        if (char.IsDigit(c) || (c == '.' && char.IsDigit(LookAhead())))
            return ReadNumericLiteral(startLine, startCol, startPos);

        // ── hex/octal literals &H… &O… ───────────────────────────────────
        if (c == '&' && (LookAhead() == 'H' || LookAhead() == 'h' || LookAhead() == 'O' || LookAhead() == 'o'))
            return ReadHexOctLiteral(startLine, startCol, startPos);

        // ── identifier or keyword ─────────────────────────────────────────
        if (char.IsLetter(c) || c == '_')
            return ReadIdentifierOrKeyword(startLine, startCol, startPos);

        // ── operators / punctuation ───────────────────────────────────────
        return ReadOperatorOrPunctuation(startLine, startCol, startPos);
    }

    // ── line comment ─────────────────────────────────────────────────────────

    private Token ReadLineComment(int startLine, int startCol, int startPos)
    {
        var sb = new StringBuilder();
        while (_pos < _source.Length && Current != '\r' && Current != '\n')
            sb.Append(Advance());
        return new Token(TokenKind.Comment, sb.ToString(), sb.ToString(), startLine, startCol, startPos);
    }

    // ── string literal ────────────────────────────────────────────────────────

    private Token ReadStringLiteral(int startLine, int startCol, int startPos)
    {
        var raw   = new StringBuilder();
        var value = new StringBuilder();
        raw.Append(Advance()); // opening "

        while (_pos < _source.Length)
        {
            char ch = Advance();
            raw.Append(ch);
            if (ch == '"')
            {
                if (Current == '"') { raw.Append(Advance()); value.Append('"'); } // escaped ""
                else break; // end of string
            }
            else value.Append(ch);
        }
        return new Token(TokenKind.StringLiteral, raw.ToString(), value.ToString(), startLine, startCol, startPos);
    }

    // ── date literal or standalone # ─────────────────────────────────────────

    private Token ReadDateOrHash(int startLine, int startCol, int startPos)
    {
        // Peek ahead: if there is a matching closing # on this line, treat as date
        int peek = _pos + 1;
        while (peek < _source.Length && _source[peek] != '#' && _source[peek] != '\r' && _source[peek] != '\n')
            peek++;

        if (peek < _source.Length && _source[peek] == '#')
        {
            var sb = new StringBuilder();
            sb.Append(Advance()); // opening #
            while (Current != '#') sb.Append(Advance());
            sb.Append(Advance()); // closing #
            return new Token(TokenKind.DateLiteral, sb.ToString(), sb.ToString(), startLine, startCol, startPos);
        }
        else
        {
            Advance();
            return new Token(TokenKind.Hash, "#", "#", startLine, startCol, startPos);
        }
    }

    // ── numeric literal ───────────────────────────────────────────────────────

    private Token ReadNumericLiteral(int startLine, int startCol, int startPos)
    {
        var sb = new StringBuilder();
        bool hasDot = false, hasExp = false;

        while (_pos < _source.Length)
        {
            char ch = Current;
            if (char.IsDigit(ch)) { sb.Append(Advance()); }
            else if (ch == '.' && !hasDot && char.IsDigit(LookAhead()))
            {
                hasDot = true;
                sb.Append(Advance());
            }
            else if ((ch == 'E' || ch == 'e') && !hasExp)
            {
                hasExp = true;
                sb.Append(Advance());
                if (Current == '+' || Current == '-') sb.Append(Advance());
            }
            else break;
        }

        // Optional type suffix
        TokenKind kind = TokenKind.DoubleLiteral;
        if (_pos < _source.Length)
        {
            char suffix = Current;
            if (suffix == '%') { sb.Append(Advance()); kind = TokenKind.IntegerLiteral; }
            else if (suffix == '&') { sb.Append(Advance()); kind = TokenKind.LongLiteral; }
            else if (suffix == '!') { sb.Append(Advance()); kind = TokenKind.SingleLiteral; }
            else if (suffix == '#') { sb.Append(Advance()); kind = TokenKind.DoubleLiteral; }
            else if (suffix == '@') { sb.Append(Advance()); kind = TokenKind.CurrencyLiteral; }
            else if (!hasDot && !hasExp) kind = TokenKind.IntegerLiteral;
        }
        else if (!hasDot && !hasExp) kind = TokenKind.IntegerLiteral;

        string text = sb.ToString();
        return new Token(kind, text, text, startLine, startCol, startPos);
    }

    // ── hex/octal ─────────────────────────────────────────────────────────────

    private Token ReadHexOctLiteral(int startLine, int startCol, int startPos)
    {
        var sb = new StringBuilder();
        sb.Append(Advance()); // &
        sb.Append(Advance()); // H or O
        while (_pos < _source.Length && (char.IsLetterOrDigit(Current)))
            sb.Append(Advance());
        if (Current == '&') sb.Append(Advance()); // long suffix
        string text = sb.ToString();
        return new Token(TokenKind.LongLiteral, text, text, startLine, startCol, startPos);
    }

    // ── identifier or keyword ─────────────────────────────────────────────────

    private Token ReadIdentifierOrKeyword(int startLine, int startCol, int startPos)
    {
        var sb = new StringBuilder();
        while (_pos < _source.Length && (char.IsLetterOrDigit(Current) || Current == '_'))
            sb.Append(Advance());

        // Optional type-declaration character
        string typeChar = string.Empty;
        if (_pos < _source.Length && "!#$%&@".IndexOf(Current) >= 0)
        {
            typeChar = Current.ToString();
            sb.Append(Advance());
        }

        string raw   = sb.ToString();
        string lower = raw.ToLowerInvariant();

        // Two-word synthetic tokens: "End If", "End Select", "End With", "End Sub", etc.
        // We handle those in the parser by peeking at the next token, not here.
        // The Rem keyword introduces a comment line.
        if (lower == "rem")
        {
            // consume rest of line as comment text
            var comment = new StringBuilder("Rem");
            while (_pos < _source.Length && Current != '\r' && Current != '\n')
                comment.Append(Advance());
            return new Token(TokenKind.Comment, comment.ToString(), comment.ToString(), startLine, startCol, startPos);
        }

        if (Keywords.TryGetValue(lower, out var kind))
            return new Token(kind, raw, lower, startLine, startCol, startPos);

        return new Token(TokenKind.Identifier, raw, lower, startLine, startCol, startPos);
    }

    // ── operators and punctuation ─────────────────────────────────────────────

    private Token ReadOperatorOrPunctuation(int startLine, int startCol, int startPos)
    {
        char c = Advance();
        return c switch
        {
            '(' => new Token(TokenKind.LeftParen,  "(", "(", startLine, startCol, startPos),
            ')' => new Token(TokenKind.RightParen, ")", ")", startLine, startCol, startPos),
            ',' => new Token(TokenKind.Comma,      ",", ",", startLine, startCol, startPos),
            '.' => new Token(TokenKind.Dot,        ".", ".", startLine, startCol, startPos),
            '!' => new Token(TokenKind.Bang,       "!", "!", startLine, startCol, startPos),
            '+' => new Token(TokenKind.Plus,       "+", "+", startLine, startCol, startPos),
            '-' => new Token(TokenKind.Minus,      "-", "-", startLine, startCol, startPos),
            '*' => new Token(TokenKind.Star,       "*", "*", startLine, startCol, startPos),
            '/' => new Token(TokenKind.Slash,      "/", "/", startLine, startCol, startPos),
            '\\' => new Token(TokenKind.BackSlash, "\\","\\",startLine, startCol, startPos),
            '^' => new Token(TokenKind.Caret,      "^", "^", startLine, startCol, startPos),
            ':' => new Token(TokenKind.Colon,      ":", ":", startLine, startCol, startPos),
            '&' => new Token(TokenKind.Ampersand,  "&", "&", startLine, startCol, startPos),
            '=' => new Token(TokenKind.Equals,     "=", "=", startLine, startCol, startPos),
            '<' => ReadCompoundOp('<', startLine, startCol, startPos),
            '>' => ReadCompoundOp('>', startLine, startCol, startPos),
            '_' =>
                // Bare underscore that is NOT a line-continuation (line-continuation would
                // have been consumed as whitespace by the caller). Emit as identifier part.
                new Token(TokenKind.Identifier, "_", "_", startLine, startCol, startPos),
            _ => new Token(TokenKind.Unknown, c.ToString(), c.ToString(), startLine, startCol, startPos),
        };
    }

    private Token ReadCompoundOp(char first, int startLine, int startCol, int startPos)
    {
        if (Current == '=')
        {
            Advance();
            return first == '<'
                ? new Token(TokenKind.LessOrEqual,    "<=", "<=", startLine, startCol, startPos)
                : new Token(TokenKind.GreaterOrEqual, ">=", ">=", startLine, startCol, startPos);
        }
        if (first == '<' && Current == '>')
        {
            Advance();
            return new Token(TokenKind.NotEquals, "<>", "<>", startLine, startCol, startPos);
        }
        return first == '<'
            ? new Token(TokenKind.LessThan,    "<", "<", startLine, startCol, startPos)
            : new Token(TokenKind.GreaterThan, ">", ">", startLine, startCol, startPos);
    }
}
