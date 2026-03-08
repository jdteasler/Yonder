using Vb6Lint.Core.Analysis;
using Vb6Lint.Rules;
using Xunit;

namespace Vb6Lint.Tests;

/// <summary>
/// Integration-style tests that run individual analyzers against short VB6
/// snippets and assert which diagnostics are (or are not) reported.
/// </summary>
public class AnalyzerTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static AnalysisResult Run<TAnalyzer>(string vb6Source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var host = new AnalyzerHost();
        host.AddAnalyzer(new TAnalyzer());
        return host.Analyze(vb6Source, "test.bas");
    }

    private static void AssertRuleId(AnalysisResult result, string ruleId)
        => Assert.Contains(result.Diagnostics, d => d.Descriptor.Id == ruleId);

    private static void AssertNoRuleId(AnalysisResult result, string ruleId)
        => Assert.DoesNotContain(result.Diagnostics, d => d.Descriptor.Id == ruleId);

    // ── VB6001: Missing Option Explicit ──────────────────────────────────────

    [Fact]
    public void VB6001_FiresWhenOptionExplicitMissing()
    {
        var src = """
            Sub Test()
            End Sub
            """;
        AssertRuleId(Run<MissingOptionExplicitAnalyzer>(src), "VB6001");
    }

    [Fact]
    public void VB6001_SilentWhenOptionExplicitPresent()
    {
        var src = """
            Option Explicit
            Sub Test()
            End Sub
            """;
        AssertNoRuleId(Run<MissingOptionExplicitAnalyzer>(src), "VB6001");
    }

    // ── VB6002: Unguarded On Error Resume Next ────────────────────────────────

    [Fact]
    public void VB6002_FiresWhenErrNotChecked()
    {
        var src = """
            Sub Test()
                On Error Resume Next
                MsgBox "done"
            End Sub
            """;
        AssertRuleId(Run<OnErrorResumeNextAnalyzer>(src), "VB6002");
    }

    [Fact]
    public void VB6002_SilentWhenErrIsChecked()
    {
        var src = """
            Sub Test()
                On Error Resume Next
                Dim x As Integer
                x = 1 / 0
                If Err.Number <> 0 Then
                    Err.Clear
                End If
            End Sub
            """;
        AssertNoRuleId(Run<OnErrorResumeNextAnalyzer>(src), "VB6002");
    }

    // ── VB6003: GoTo ──────────────────────────────────────────────────────────

    [Fact]
    public void VB6003_FiresForFlowControlGoTo()
    {
        var src = """
            Sub Test()
                GoTo MyLabel
            MyLabel:
            End Sub
            """;
        AssertRuleId(Run<GoToAnalyzer>(src), "VB6003");
    }

    [Fact]
    public void VB6003_SilentForErrorHandlerGoTo()
    {
        var src = """
            Sub Test()
                On Error GoTo ErrHandler
                Dim x As Integer
                x = 1 / 0
                Exit Sub
            ErrHandler:
                MsgBox Err.Description
            End Sub
            """;
        AssertNoRuleId(Run<GoToAnalyzer>(src), "VB6003");
    }

    // ── VB6005: Unused local variable ────────────────────────────────────────

    [Fact]
    public void VB6005_FiresForDeclaredButNeverUsedVariable()
    {
        var src = """
            Option Explicit
            Sub Test()
                Dim unused As Integer
            End Sub
            """;
        AssertRuleId(Run<UnusedVariableAnalyzer>(src), "VB6005");
    }

    [Fact]
    public void VB6005_SilentWhenVariableIsUsed()
    {
        var src = """
            Option Explicit
            Sub Test()
                Dim x As Integer
                x = 5
                MsgBox CStr(x)
            End Sub
            """;
        AssertNoRuleId(Run<UnusedVariableAnalyzer>(src), "VB6005");
    }

    // ── VB6007: Unused private procedure ─────────────────────────────────────

    [Fact]
    public void VB6007_FiresForPrivateProcedureNeverCalled()
    {
        var src = """
            Option Explicit
            Private Sub DeadCode()
                MsgBox "never"
            End Sub
            Public Sub Main()
            End Sub
            """;
        AssertRuleId(Run<UnusedProcedureAnalyzer>(src), "VB6007");
    }

    [Fact]
    public void VB6007_SilentWhenPrivateProcedureIsCalled()
    {
        var src = """
            Option Explicit
            Private Sub Helper()
                MsgBox "hi"
            End Sub
            Public Sub Main()
                Helper
            End Sub
            """;
        AssertNoRuleId(Run<UnusedProcedureAnalyzer>(src), "VB6007");
    }

    // ── VB6008: Implicit type coercion ────────────────────────────────────────

    [Fact]
    public void VB6008_FiresWhenStringAssignedToInteger()
    {
        var src = """
            Option Explicit
            Sub Test()
                Dim n As Integer
                n = "123"
            End Sub
            """;
        AssertRuleId(Run<ImplicitTypeCoercionAnalyzer>(src), "VB6008");
    }

    // ── VB6009: Plus for string concatenation ────────────────────────────────

    [Fact]
    public void VB6009_FiresWhenPlusUsedForStringConcat()
    {
        var src = """
            Option Explicit
            Sub Test()
                Dim a As String
                Dim b As String
                Dim c As String
                a = "hello"
                b = "world"
                c = a + b
            End Sub
            """;
        AssertRuleId(Run<ImplicitTypeCoercionAnalyzer>(src), "VB6009");
    }

    [Fact]
    public void VB6009_SilentWhenAmpersandUsedForConcat()
    {
        var src = """
            Option Explicit
            Sub Test()
                Dim a As String
                Dim b As String
                Dim c As String
                a = "hello"
                b = "world"
                c = a & b
            End Sub
            """;
        AssertNoRuleId(Run<ImplicitTypeCoercionAnalyzer>(src), "VB6009");
    }
}
