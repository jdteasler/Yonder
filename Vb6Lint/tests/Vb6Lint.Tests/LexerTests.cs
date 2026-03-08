using Vb6Lint.Core.Lexer;
using Xunit;

namespace Vb6Lint.Tests;

public class LexerTests
{
    private static IReadOnlyList<Token> Lex(string src)
        => new Vb6Lexer(src).Tokenize();

    [Fact]
    public void EmptySource_ReturnsOnlyEof()
    {
        var tokens = Lex("");
        Assert.Single(tokens);
        Assert.Equal(TokenKind.EndOfFile, tokens[0].Kind);
    }

    [Fact]
    public void Keywords_AreIdentifiedCorrectly()
    {
        var tokens = Lex("Dim Sub Function End");
        var kinds  = tokens.Where(t => t.Kind != TokenKind.NewLine && t.Kind != TokenKind.EndOfFile)
                           .Select(t => t.Kind).ToList();
        Assert.Equal(new[] { TokenKind.Dim, TokenKind.Sub, TokenKind.Function, TokenKind.End }, kinds);
    }

    [Fact]
    public void Keywords_AreCaseInsensitive()
    {
        var tokens = Lex("DIM dim DiM");
        Assert.All(tokens.Where(t => t.Kind != TokenKind.NewLine && t.Kind != TokenKind.EndOfFile),
            t => Assert.Equal(TokenKind.Dim, t.Kind));
    }

    [Fact]
    public void StringLiteral_IsLexedCorrectly()
    {
        var tokens = Lex("\"Hello, world!\"");
        var str = tokens.First(t => t.Kind == TokenKind.StringLiteral);
        Assert.Equal("Hello, world!", str.Value);
    }

    [Fact]
    public void StringLiteral_EscapedQuote()
    {
        var tokens = Lex("\"say \"\"hi\"\"\"");
        var str = tokens.First(t => t.Kind == TokenKind.StringLiteral);
        Assert.Equal("say \"hi\"", str.Value);
    }

    [Fact]
    public void IntegerLiteral_IsRecognised()
    {
        var tokens = Lex("42");
        Assert.Equal(TokenKind.IntegerLiteral, tokens.First(t => t.Kind != TokenKind.EndOfFile).Kind);
    }

    [Fact]
    public void Comment_IsLexed()
    {
        var tokens = Lex("' This is a comment");
        Assert.Contains(tokens, t => t.Kind == TokenKind.Comment);
    }

    [Fact]
    public void Operators_AreLexedCorrectly()
    {
        var tokens = Lex("<> <= >= < >");
        var ops    = tokens.Where(t => t.Kind != TokenKind.NewLine && t.Kind != TokenKind.EndOfFile)
                           .Select(t => t.Kind).ToList();
        Assert.Contains(TokenKind.NotEquals,       ops);
        Assert.Contains(TokenKind.LessOrEqual,     ops);
        Assert.Contains(TokenKind.GreaterOrEqual,  ops);
        Assert.Contains(TokenKind.LessThan,        ops);
        Assert.Contains(TokenKind.GreaterThan,     ops);
    }

    [Fact]
    public void LineNumbers_AreTracked()
    {
        var tokens = Lex("Dim x\nDim y");
        var dimY   = tokens.First(t => t.Kind == TokenKind.Dim && t.Line == 2);
        Assert.Equal(2, dimY.Line);
    }
}
