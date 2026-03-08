namespace Vb6Lint.Core.Lexer;

/// <summary>
/// An immutable token produced by <see cref="Vb6Lexer"/>.
/// Carries its source position so diagnostics can cite file/line/column.
/// </summary>
public sealed class Token
{
    public TokenKind Kind      { get; }
    public string    Text      { get; }   // raw source text of the token
    public string    Value     { get; }   // normalised value (e.g. lower-case keyword, unquoted string)
    public int       Line      { get; }   // 1-based
    public int       Column    { get; }   // 1-based
    public int       Position  { get; }   // 0-based absolute character offset

    public Token(TokenKind kind, string text, string value, int line, int column, int position)
    {
        Kind     = kind;
        Text     = text;
        Value    = value;
        Line     = line;
        Column   = column;
        Position = position;
    }

    public override string ToString() => $"[{Kind} \"{Text}\" L{Line}:C{Column}]";
}
