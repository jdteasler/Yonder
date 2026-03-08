Attribute VB_Name = "BadModule"
' ============================================================
' BadModule.bas — sample VB6 file that intentionally triggers
' every built-in vb6lint rule.  Use it to verify the tool.
' ============================================================

' VB6001: Option Explicit is deliberately ABSENT
' -------------------------------------------------
' If this line were present no VB6001 would fire:
'   Option Explicit


' ── VB6002: On Error Resume Next without Err check ───────────
Public Sub SwallowErrors()
    On Error Resume Next          ' <- VB6002: Err is never checked below
    Dim result As Integer
    result = 1 / 0                ' division by zero — silently ignored
    MsgBox "Done"
End Sub


' ── VB6002 COMPLIANT: On Error Resume Next WITH Err check ────
Public Sub ProperErrorHandling()
    On Error Resume Next
    Dim result As Integer
    result = 1 / 0
    If Err.Number <> 0 Then       ' Err checked — VB6002 does NOT fire
        MsgBox "Error: " & Err.Description
        Err.Clear
    End If
End Sub


' ── VB6003: GoTo used for flow control (not error handler) ───
Public Sub UseGoTo()
    Dim i As Integer
    i = 0
LoopTop:                          ' label
    i = i + 1
    If i < 5 Then GoTo LoopTop    ' <- VB6003: GoTo used as loop

    MsgBox "i = " & CStr(i)
End Sub


' ── VB6003 ALLOWED: GoTo targeting an error-handler label ────
Public Sub AllowedGoTo()
    On Error GoTo ErrHandler
    Dim x As Integer
    x = 10 / 0
    Exit Sub
ErrHandler:
    MsgBox "Caught: " & Err.Description
End Sub


' ── VB6004: GoSub ─────────────────────────────────────────────
Public Sub UseGoSub()
    GoSub Greet               ' <- VB6004
    Exit Sub
Greet:
    MsgBox "Hello"
    Return
End Sub


' ── VB6005: Unused local variable ────────────────────────────
Public Sub HasUnusedLocals()
    Dim usedVar   As String
    Dim unusedVar As Integer      ' <- VB6005: never referenced

    usedVar = "hello"
    MsgBox usedVar
End Sub


' ── VB6006: Write-only local variable ─────────────────────────
Public Sub HasWriteOnlyLocal()
    Dim sink As Long              ' <- VB6006: assigned but value never read
    sink = 42
    MsgBox "Done"
End Sub


' ── VB6007: Unused private procedure ─────────────────────────
Private Sub NeverCalled()         ' <- VB6007
    MsgBox "I am dead code"
End Sub


' ── VB6008: Implicit string-to-numeric coercion ───────────────
Public Sub ImplicitCoercion()
    Dim total As Integer
    total = "123"                 ' <- VB6008: String literal → Integer
    MsgBox CStr(total)
End Sub


' ── VB6009: Using '+' for string concatenation ───────────────
Public Sub PlusConcat()
    Dim firstName As String
    Dim lastName  As String
    firstName = "John"
    lastName  = "Doe"
    Dim fullName As String
    fullName = firstName + lastName  ' <- VB6009: use & instead of +
    MsgBox fullName
End Sub


' ── CLEAN: procedure that should produce no warnings ──────────
Public Function AddNumbers(ByVal a As Integer, ByVal b As Integer) As Integer
    AddNumbers = a + b
End Function
