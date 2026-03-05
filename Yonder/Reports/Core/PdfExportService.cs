namespace Yonder.Reports.Core;

/// <summary>
/// Orchestrates the full PDF export pipeline:
/// 1. Resolve the report by key
/// 2. Call FetchDataAsync with the supplied parameters
/// 3. Call ExportPdf to produce the byte array
/// </summary>
public class PdfExportService
{
    private readonly ReportRegistry _registry;
    private readonly IServiceProvider _services;

    public PdfExportService(ReportRegistry registry, IServiceProvider services)
    {
        _registry = registry;
        _services = services;
    }

    /// <summary>Returns raw PDF bytes for the named report.</summary>
    /// <param name="reportKey">Matches IReport.Key (e.g. "attendance-daily").</param>
    /// <param name="parameters">Instance of the report's ParametersType.</param>
    public async Task<byte[]> ExportAsync(string reportKey, object parameters)
    {
        var report = _registry.GetReport(reportKey)
            ?? throw new KeyNotFoundException($"Report '{reportKey}' is not registered.");

        var data = await report.FetchDataAsync(parameters, _services);
        return report.ExportPdf(data, parameters);
    }
}
