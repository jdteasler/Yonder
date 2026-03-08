# vb6lint

A **Roslyn-inspired static analysis tool for Visual Basic 6** (`.bas`, `.cls`, `.frm`) written in C# / .NET 6+.

`vb6lint` parses VB6 source code into a full abstract syntax tree, then runs a configurable set of diagnostic rules (analyzers) over that tree to report warnings and errors — all without executing the code.

---

## Architecture

```
Vb6Lint/
├── src/
│   ├── Vb6Lint.Core/           Core library (no external dependencies)
│   │   ├── Lexer/
│   │   │   ├── TokenKind.cs    Enumeration of all VB6 token types
│   │   │   ├── Token.cs        Immutable token with source position
│   │   │   └── Vb6Lexer.cs     Hand-written lexer; handles line continuations,
│   │   │                        comments, date/string literals, hex/octal
│   │   ├── Syntax/
│   │   │   └── SyntaxNode.cs   All AST node types (CompilationUnit, Procedure,
│   │   │                        Variable, If, For, Do, Select, GoTo, expressions…)
│   │   ├── Parser/
│   │   │   └── Vb6Parser.cs    Recursive-descent parser with error recovery
│   │   └── Analysis/
│   │       ├── DiagnosticSeverity.cs      Info / Warning / Error
│   │       ├── DiagnosticDescriptor.cs    Rule metadata (ID, title, format, …)
│   │       ├── Diagnostic.cs              A reported finding (file, line, col, msg)
│   │       ├── DiagnosticAnalyzer.cs      Abstract base class for rules
│   │       ├── AnalysisContext.cs         Callback registration (node + compilation)
│   │       ├── SyntaxWalker.cs            Depth-first AST visitor
│   │       ├── SyntaxTreeExtensions.cs    DescendantsAndSelf<T>(), ReferencedNames()
│   │       └── AnalyzerHost.cs            Orchestrates lex → parse → walk → report
│   │
│   ├── Vb6Lint.Rules/          Built-in rule library
│   │   ├── MissingOptionExplicitAnalyzer.cs   VB6001
│   │   ├── OnErrorResumeNextAnalyzer.cs        VB6002
│   │   ├── GoToAnalyzer.cs                     VB6003, VB6004
│   │   ├── UnusedVariableAnalyzer.cs           VB6005, VB6006
│   │   ├── UnusedProcedureAnalyzer.cs          VB6007
│   │   ├── ImplicitTypeCoercionAnalyzer.cs     VB6008, VB6009
│   │   └── BuiltInRules.cs                     Factory: BuiltInRules.All()
│   │
│   └── Vb6Lint.Cli/            CLI entry point
│       └── Program.cs          Argument parsing, file collection, output formatting
│
├── tests/
│   └── Vb6Lint.Tests/
│       ├── LexerTests.cs       Unit tests for the lexer
│       ├── ParserTests.cs      Unit tests for the parser
│       └── AnalyzerTests.cs    Integration tests: each rule fires / stays silent
│
└── samples/
    ├── BadModule.bas   Intentionally buggy VB6 — triggers every built-in rule
    └── CleanModule.bas Well-written VB6 — should produce zero diagnostics
```

### Design principles (Roslyn-inspired)

| Roslyn concept | vb6lint equivalent |
|---|---|
| `SyntaxTree` | `CompilationUnitSyntax` |
| `SyntaxNode` | `SyntaxNode` base class + all concrete node types |
| `DiagnosticAnalyzer` | `DiagnosticAnalyzer` abstract class |
| `DiagnosticDescriptor` | `DiagnosticDescriptor` |
| `Diagnostic` | `Diagnostic` |
| `AnalysisContext.RegisterSyntaxNodeAction<T>` | `AnalysisContext.RegisterSyntaxNodeAction<TNode>` |
| `CompilationStartAnalysisContext` | `AnalysisContext.RegisterCompilationAction` |
| `AnalyzerDriver` | `AnalyzerHost` |

---

## Built-in rules

| Rule ID | Severity | Category | Description |
|---------|----------|----------|-------------|
| `VB6001` | Warning | Correctness | Missing `Option Explicit` |
| `VB6002` | Warning | Correctness | `On Error Resume Next` without checking `Err` |
| `VB6003` | Warning | Style | `GoTo` used for flow control (not error handler) |
| `VB6004` | Warning | Style | `GoSub` — obsolete subroutine mechanism |
| `VB6005` | Warning | Usage | Local variable declared but never used |
| `VB6006` | Info    | Usage | Local variable assigned but value never read |
| `VB6007` | Warning | Usage | Private procedure never called in this module |
| `VB6008` | Warning | Correctness | Implicit string-to-numeric coercion |
| `VB6009` | Warning | Style | `+` used for string concatenation (use `&`) |
| `VB6000` | Error   | Syntax | Parse error (internal) |

---

## Building

### Prerequisites

* [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or later

### Build

```bash
cd Vb6Lint
dotnet build Vb6Lint.sln
```

### Run tests

```bash
dotnet test Vb6Lint.sln
```

### Publish a self-contained executable

```bash
dotnet publish src/Vb6Lint.Cli/Vb6Lint.Cli.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o ./publish/win-x64

# Linux
dotnet publish src/Vb6Lint.Cli/Vb6Lint.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o ./publish/linux-x64
```

---

## Usage

```
vb6lint [options] <file|directory> [...]

Options:
  --format <msbuild|text|json>     Output format           (default: msbuild)
  --severity <error|warning|info>  Minimum severity        (default: warning)
  --rules <ID,ID,...>              Enable only these rules (default: all)
  --exclude <ID,ID,...>            Suppress these rule IDs
  --no-parse-errors               Suppress VB6000 parse-error messages
  --recursive                      Scan directories recursively
  --list-rules                     List all built-in rules and exit
  --version                        Print version and exit
  --help, -h                       Show this help
```

### Examples

```bash
# Analyse a single file (MSBuild output — works in any CI pipeline)
vb6lint src/MyModule.bas

# Analyse a directory, show human-readable output
vb6lint --format text ./legacy

# Only report actual errors, suppress warnings
vb6lint --severity error ./src

# Suppress GoTo warnings for a legacy codebase
vb6lint --exclude VB6003,VB6004 ./src

# Run only option-explicit and unused-variable checks
vb6lint --rules VB6001,VB6005 ./src

# JSON report for integration with other tooling
vb6lint --format json ./src > lint-report.json
```

### Sample output (MSBuild format)

```
samples/BadModule.bas(1,1): warning VB6001: Module 'BadModule.bas' is missing 'Option Explicit'. Without it, undeclared variables are allowed and typos create silent bugs.
samples/BadModule.bas(14,5): warning VB6002: 'On Error Resume Next' in 'swallowindexers' suppresses all errors. Check Err.Number after each operation that may fail, or use 'On Error GoTo <label>' for structured handling.
samples/BadModule.bas(42,5): warning VB6003: Use of 'GoTo LoopTop' creates unstructured control flow. Replace with structured constructs (If/Else, Do/Loop, Exit Sub) where possible.
samples/BadModule.bas(55,5): warning VB6004: 'GoSub' is an obsolete subroutine-call mechanism. Replace with a proper Private Sub or Function.
samples/BadModule.bas(63,9): warning VB6005: Local variable 'unusedVar' in 'hasunusedlocals' is declared but never used.
samples/BadModule.bas(73,9): info   VB6006: Local variable 'sink' in 'haswriteonlylocal' is assigned but its value is never read.
samples/BadModule.bas(80,1): warning VB6007: Private Sub 'nevercalled' is never called within this module.
samples/BadModule.bas(87,9): warning VB6008: Assigning a String value to 'total' (declared as Integer) relies on implicit coercion. Use an explicit conversion function such as CInt(), CLng(), CDbl().
samples/BadModule.bas(99,18): warning VB6009: Using '+' for string concatenation is ambiguous. When either operand is a String, use '&' to make the intent explicit and avoid numeric-addition surprises.

Analysed 1 file(s): 0 error(s), 8 warning(s), 1 info(s).
```

---

## Writing a custom rule

Rules are just subclasses of `DiagnosticAnalyzer` in any assembly.  Here is a
complete example that warns when `MsgBox` is used (a common "don't ship debug
dialogs" rule):

```csharp
using Vb6Lint.Core.Analysis;
using Vb6Lint.Core.Syntax;

public sealed class NoMsgBoxAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id:              "CUSTOM001",
        title:           "Avoid MsgBox in production code",
        messageFormat:   "MsgBox call found in '{0}'. Remove before release.",
        category:        "Usage",
        defaultSeverity: DiagnosticSeverity.Warning);

    public override IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = new[] { Rule };

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction<CallStatementSyntax>(ctx =>
        {
            var call = (CallStatementSyntax)ctx.Node;
            if (call.Target is IdentifierExpressionSyntax id &&
                string.Equals(id.Name, "msgbox", StringComparison.OrdinalIgnoreCase))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    ctx.FilePath,
                    call.Line,
                    call.Column,
                    id.Name));
            }
        });
    }
}
```

Register it at startup:

```csharp
var host = new AnalyzerHost();
host.AddAnalyzers(BuiltInRules.All());
host.AddAnalyzer(new NoMsgBoxAnalyzer());

var result = host.Analyze(File.ReadAllText("MyModule.bas"), "MyModule.bas");
foreach (var d in result.Diagnostics)
    Console.WriteLine(d.ToMsBuildString());
```

---

## CI integration

Because the tool exits with code `1` when any **error**-severity diagnostics are
emitted and `0` otherwise, it slots directly into any pipeline:

```yaml
# Azure Pipelines
- task: DotNetCoreCLI@2
  displayName: Run VB6 Lint
  inputs:
    command: run
    projects: 'Vb6Lint/src/Vb6Lint.Cli/Vb6Lint.Cli.csproj'
    arguments: '-- --format msbuild $(Build.SourcesDirectory)/legacy'
```

```yaml
# GitHub Actions
- name: Run vb6lint
  run: dotnet run --project Vb6Lint/src/Vb6Lint.Cli -- --format msbuild ./legacy
```

---

## License

MIT
