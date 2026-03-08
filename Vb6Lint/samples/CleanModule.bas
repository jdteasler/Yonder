Attribute VB_Name = "CleanModule"
' ============================================================
' CleanModule.bas — a well-written VB6 module that should
' produce zero vb6lint warnings.
' ============================================================

Option Explicit

' ── Module-level constants ────────────────────────────────────
Private Const MaxRetries As Integer = 3

' ── Public API ───────────────────────────────────────────────

''' Returns the greater of two integers.
Public Function Max(ByVal a As Integer, ByVal b As Integer) As Integer
    If a > b Then
        Max = a
    Else
        Max = b
    End If
End Function


''' Safely divides two numbers, returning 0 on division by zero.
Public Function SafeDivide(ByVal numerator As Double, _
                            ByVal denominator As Double) As Double
    On Error GoTo DivideError
    SafeDivide = numerator / denominator
    Exit Function
DivideError:
    SafeDivide = 0
End Function


''' Reads a file and returns its content, or empty string on failure.
Public Function ReadFile(ByVal filePath As String) As String
    Dim fileNum As Integer
    Dim content As String
    Dim line    As String

    On Error GoTo ReadError

    fileNum = FreeFile()
    Open filePath For Input As #fileNum
    Do While Not EOF(fileNum)
        Line Input #fileNum, line
        content = content & line & vbCrLf
    Loop
    Close #fileNum

    ReadFile = content
    Exit Function

ReadError:
    If Err.Number <> 0 Then
        ReadFile = vbNullString
        Err.Clear
    End If
End Function


''' Builds a full name string.
Public Function FullName(ByVal firstName As String, ByVal lastName As String) As String
    FullName = firstName & " " & lastName
End Function


''' Iterates a collection and returns a count.
Public Function CountItems(ByVal col As Collection) As Long
    Dim item     As Variant
    Dim counter  As Long

    counter = 0
    For Each item In col
        counter = counter + 1
    Next item

    CountItems = counter
End Function
