using Vb6Lint.Core.Lexer;
using Vb6Lint.Core.Parser;
using Vb6Lint.Core.Syntax;
using Xunit;

namespace Vb6Lint.Tests;

public class ParserTests
{
    private static CompilationUnitSyntax Parse(string src)
    {
        var tokens = new Vb6Lexer(src).Tokenize();
        return new Vb6Parser(tokens).Parse("test.bas");
    }

    [Fact]
    public void ParseOptionExplicit()
    {
        var unit = Parse("Option Explicit\n");
        var opt  = unit.Members.OfType<OptionStatementSyntax>().FirstOrDefault();
        Assert.NotNull(opt);
        Assert.Equal("Explicit", opt!.OptionType);
    }

    [Fact]
    public void ParseSimpleSub()
    {
        var unit = Parse("""
            Sub HelloWorld()
                MsgBox "Hello"
            End Sub
            """);

        var proc = unit.Members.OfType<ProcedureDeclarationSyntax>().FirstOrDefault();
        Assert.NotNull(proc);
        Assert.Equal("helloworld", proc!.Name);
        Assert.Equal(ProcedureKind.Sub, proc.Kind);
    }

    [Fact]
    public void ParseFunction_WithReturnType()
    {
        var unit = Parse("""
            Function Add(ByVal a As Integer, ByVal b As Integer) As Integer
                Add = a + b
            End Function
            """);

        var proc = unit.Members.OfType<ProcedureDeclarationSyntax>().First();
        Assert.Equal(ProcedureKind.Function, proc.Kind);
        Assert.Equal(2, proc.Parameters.Count);
        Assert.Equal("Integer", proc.ReturnType);
    }

    [Fact]
    public void ParseDimStatement()
    {
        var unit = Parse("""
            Sub Test()
                Dim x As Integer
            End Sub
            """);
        var proc = unit.Members.OfType<ProcedureDeclarationSyntax>().First();
        var dim  = proc.Body.OfType<VariableDeclarationSyntax>().FirstOrDefault();
        Assert.NotNull(dim);
        Assert.Equal("x", dim!.Declarators[0].Name);
        Assert.Equal("Integer", dim.Declarators[0].TypeName);
    }

    [Fact]
    public void ParseOnErrorResumeNext()
    {
        var unit = Parse("""
            Sub Test()
                On Error Resume Next
            End Sub
            """);
        var proc = unit.Members.OfType<ProcedureDeclarationSyntax>().First();
        var onErr = proc.Body.OfType<OnErrorStatementSyntax>().FirstOrDefault();
        Assert.NotNull(onErr);
        Assert.Equal(OnErrorKind.ResumeNext, onErr!.Kind);
    }

    [Fact]
    public void ParseOnErrorGoTo()
    {
        var unit = Parse("""
            Sub Test()
                On Error GoTo ErrHandler
            ErrHandler:
            End Sub
            """);
        var proc  = unit.Members.OfType<ProcedureDeclarationSyntax>().First();
        var onErr = proc.Body.OfType<OnErrorStatementSyntax>().FirstOrDefault();
        Assert.NotNull(onErr);
        Assert.Equal(OnErrorKind.GoToLabel, onErr!.Kind);
        Assert.Equal("errhandler", onErr.GotoLabel);
    }

    [Fact]
    public void ParseIfStatement()
    {
        var unit = Parse("""
            Sub Test()
                If x > 0 Then
                    MsgBox "positive"
                Else
                    MsgBox "non-positive"
                End If
            End Sub
            """);
        var proc = unit.Members.OfType<ProcedureDeclarationSyntax>().First();
        var ifStmt = proc.Body.OfType<IfStatementSyntax>().FirstOrDefault();
        Assert.NotNull(ifStmt);
        Assert.NotNull(ifStmt!.ElseBody);
    }

    [Fact]
    public void ParseForLoop()
    {
        var unit = Parse("""
            Sub Test()
                Dim i As Integer
                For i = 1 To 10
                    MsgBox CStr(i)
                Next i
            End Sub
            """);
        var proc = unit.Members.OfType<ProcedureDeclarationSyntax>().First();
        var forStmt = proc.Body.OfType<ForStatementSyntax>().FirstOrDefault();
        Assert.NotNull(forStmt);
        Assert.Equal("i", forStmt!.Counter);
    }

    [Fact]
    public void ParseGoToStatement()
    {
        var unit = Parse("""
            Sub Test()
                GoTo MyLabel
            MyLabel:
            End Sub
            """);
        var proc  = unit.Members.OfType<ProcedureDeclarationSyntax>().First();
        var goTo  = proc.Body.OfType<GoToStatementSyntax>().FirstOrDefault();
        Assert.NotNull(goTo);
        Assert.Equal("mylabel", goTo!.Label);
    }
}
