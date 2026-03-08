using System.Reflection;
using Vb6Lint.Core.Analysis;
using Vb6Lint.Rules;

namespace Vb6Lint.Cli;

/// <summary>
/// vb6lint — command-line entry point.
///
/// Usage:
///   vb6lint [options] &lt;file-or-directory&gt; [&lt;file-or-directory&gt; ...]
///
/// Options:
///   --format msbuild|text|json      Output format (default: msbuild)
///   --severity error|warning|info   Minimum severity to report (default: warning)
///   --rules VB6001,VB6002,...        Comma-separated list of rule IDs to enable
///                                    (default: all)
///   --exclude VB6001,VB6003,...      Comma-separated list of rule IDs to suppress
///   --list-rules                     Print all built-in rules and exit
///   --no-parse-errors               Suppress VB6000 (parse error) diagnostics
///   --recursive                      Recurse into sub-directories (implied when a
///                                    directory is given)
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // ── Argument parsing ─────────────────────────────────────────────────
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var options = ParseOptions(args);

        if (options.ShowHelp)    { PrintUsage(); return 0; }
        if (options.ListRules)   { ListRules();  return 0; }
        if (options.ShowVersion) { Console.WriteLine(GetVersion()); return 0; }

        if (options.Inputs.Count == 0)
        {
            Console.Error.WriteLine("error: No input files or directories specified.");
            return 1;
        }

        // ── Collect VB6 source files ─────────────────────────────────────────
        var files = CollectFiles(options.Inputs, options.Recursive);
        if (files.Count == 0)
        {
            Console.Error.WriteLine("warning: No VB6 source files (.bas, .cls, .frm) found.");
            return 0;
        }

        // ── Build analyser host ──────────────────────────────────────────────
        var host = new AnalyzerHost();
        var allRules = BuiltInRules.All();

        foreach (var rule in allRules)
        {
            bool include = options.EnabledRules.Count  == 0 ||
                           options.EnabledRules.Any(id => rule.SupportedDiagnostics.Any(d => d.Id == id));
            bool exclude = options.DisabledRules.Any(id => rule.SupportedDiagnostics.Any(d => d.Id == id));

            if (include && !exclude) host.AddAnalyzer(rule);
        }

        // ── Analyse files ────────────────────────────────────────────────────
        var allDiagnostics = new List<Diagnostic>();
        int fileCount      = 0;
        int errorCount     = 0;

        foreach (var filePath in files)
        {
            string source;
            try { source = File.ReadAllText(filePath); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: Cannot read '{filePath}': {ex.Message}");
                continue;
            }

            var result = host.Analyze(source, filePath);

            foreach (var diag in result.Diagnostics)
            {
                if (diag.Severity < options.MinSeverity) continue;
                if (options.SuppressParseErrors && diag.Descriptor.Id == "VB6000") continue;
                allDiagnostics.Add(diag);
            }

            fileCount++;
        }

        errorCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);

        // ── Output ───────────────────────────────────────────────────────────
        switch (options.Format)
        {
            case OutputFormat.MsBuild:
                OutputMsBuild(allDiagnostics);
                break;
            case OutputFormat.Text:
                OutputText(allDiagnostics);
                break;
            case OutputFormat.Json:
                OutputJson(allDiagnostics);
                break;
        }

        // ── Summary ──────────────────────────────────────────────────────────
        if (options.Format != OutputFormat.Json)
        {
            int warnCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
            int infoCount = allDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);
            Console.WriteLine();
            Console.WriteLine($"Analysed {fileCount} file(s): " +
                              $"{errorCount} error(s), {warnCount} warning(s), {infoCount} info(s).");
        }

        // Return non-zero exit code when there are errors (CI integration)
        return errorCount > 0 ? 1 : 0;
    }

    // ── Output formatters ────────────────────────────────────────────────────

    private static void OutputMsBuild(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (var d in diagnostics)
            Console.WriteLine(d.ToMsBuildString());
    }

    private static void OutputText(IEnumerable<Diagnostic> diagnostics)
    {
        string? lastFile = null;
        foreach (var d in diagnostics)
        {
            if (d.FilePath != lastFile)
            {
                Console.WriteLine();
                Console.WriteLine($"  {d.FilePath}");
                lastFile = d.FilePath;
            }
            var sev = d.Severity switch
            {
                DiagnosticSeverity.Error   => "ERR ",
                DiagnosticSeverity.Warning => "WARN",
                _                          => "INFO",
            };
            Console.WriteLine($"    [{sev}] {d.Descriptor.Id}  L{d.Line}:{d.Column}  {d.Message}");
        }
    }

    private static void OutputJson(IEnumerable<Diagnostic> diagnostics)
    {
        // Hand-roll minimal JSON to avoid a dependency on System.Text.Json options
        Console.WriteLine("[");
        var list = diagnostics.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            var d   = list[i];
            var sep = i < list.Count - 1 ? "," : "";
            Console.WriteLine($"  {{");
            Console.WriteLine($"    \"ruleId\":   \"{Esc(d.Descriptor.Id)}\",");
            Console.WriteLine($"    \"severity\": \"{d.Severity.ToString().ToLowerInvariant()}\",");
            Console.WriteLine($"    \"file\":     \"{Esc(d.FilePath)}\",");
            Console.WriteLine($"    \"line\":     {d.Line},");
            Console.WriteLine($"    \"column\":   {d.Column},");
            Console.WriteLine($"    \"message\":  \"{Esc(d.Message)}\",");
            Console.WriteLine($"    \"category\": \"{Esc(d.Descriptor.Category)}\"");
            Console.WriteLine($"  }}{sep}");
        }
        Console.WriteLine("]");

        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // ── File collection ──────────────────────────────────────────────────────

    private static List<string> CollectFiles(IEnumerable<string> inputs, bool recursive)
    {
        var files = new List<string>();
        var option = recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        foreach (var input in inputs)
        {
            if (Directory.Exists(input))
            {
                foreach (var ext in new[] { "*.bas", "*.cls", "*.frm" })
                    files.AddRange(Directory.GetFiles(input, ext, SearchOption.AllDirectories));
            }
            else if (File.Exists(input))
            {
                files.Add(Path.GetFullPath(input));
            }
            else
            {
                // Could be a glob pattern — expand manually for simple cases
                var dir     = Path.GetDirectoryName(input) ?? ".";
                var pattern = Path.GetFileName(input);
                if (Directory.Exists(dir))
                    files.AddRange(Directory.GetFiles(dir, pattern, option));
                else
                    Console.Error.WriteLine($"warning: Input path not found: '{input}'");
            }
        }

        return files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f).ToList();
    }

    // ── Help / list ──────────────────────────────────────────────────────────

    private static void PrintUsage()
    {
        Console.WriteLine("""
            vb6lint — Roslyn-inspired static analyser for Visual Basic 6

            Usage:
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

            Examples:
              vb6lint src/MyModule.bas
              vb6lint --format text ./src
              vb6lint --severity warning --exclude VB6003 ./legacy
              vb6lint --format json ./src > report.json

            Exit codes:
              0  No errors (warnings may be present)
              1  One or more error-severity diagnostics emitted
            """);
    }

    private static void ListRules()
    {
        Console.WriteLine("Built-in rules:");
        Console.WriteLine();
        foreach (var analyzer in BuiltInRules.All())
        {
            foreach (var d in analyzer.SupportedDiagnostics)
            {
                var defSev = d.DefaultSeverity.ToString().ToLowerInvariant();
                Console.WriteLine($"  {d.Id,-10} [{defSev,-7}]  {d.Category,-14} {d.Title}");
            }
        }
    }

    private static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return $"vb6lint {version?.ToString(3) ?? "1.0.0"}";
    }

    // ── Option parsing ───────────────────────────────────────────────────────

    private sealed class CliOptions
    {
        public List<string>    Inputs           { get; } = new();
        public OutputFormat    Format           { get; set; } = OutputFormat.MsBuild;
        public DiagnosticSeverity MinSeverity   { get; set; } = DiagnosticSeverity.Warning;
        public HashSet<string> EnabledRules     { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DisabledRules    { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool            Recursive        { get; set; }
        public bool            SuppressParseErrors { get; set; }
        public bool            ShowHelp         { get; set; }
        public bool            ShowVersion      { get; set; }
        public bool            ListRules        { get; set; }
    }

    private static CliOptions ParseOptions(string[] args)
    {
        var opts = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--help": case "-h": opts.ShowHelp = true; break;
                case "--version":         opts.ShowVersion = true; break;
                case "--list-rules":      opts.ListRules = true; break;
                case "--recursive":       opts.Recursive = true; break;
                case "--no-parse-errors": opts.SuppressParseErrors = true; break;

                case "--format":
                    if (++i < args.Length)
                        opts.Format = args[i].ToLowerInvariant() switch
                        {
                            "text" => OutputFormat.Text,
                            "json" => OutputFormat.Json,
                            _      => OutputFormat.MsBuild,
                        };
                    break;

                case "--severity":
                    if (++i < args.Length)
                        opts.MinSeverity = args[i].ToLowerInvariant() switch
                        {
                            "error" => DiagnosticSeverity.Error,
                            "info"  => DiagnosticSeverity.Info,
                            _       => DiagnosticSeverity.Warning,
                        };
                    break;

                case "--rules":
                    if (++i < args.Length)
                        foreach (var id in args[i].Split(',', StringSplitOptions.RemoveEmptyEntries))
                            opts.EnabledRules.Add(id.Trim());
                    break;

                case "--exclude":
                    if (++i < args.Length)
                        foreach (var id in args[i].Split(',', StringSplitOptions.RemoveEmptyEntries))
                            opts.DisabledRules.Add(id.Trim());
                    break;

                default:
                    opts.Inputs.Add(args[i]);
                    break;
            }
        }
        return opts;
    }

    private enum OutputFormat { MsBuild, Text, Json }
}
