namespace Yonder.Reports.Core;

/// <summary>
/// Central catalog of every registered report.
/// Populated at startup by DI – no manual wiring needed when you add a new report.
/// </summary>
public class ReportRegistry
{
    private readonly Dictionary<string, IReport> _reports;

    public ReportRegistry(IEnumerable<IReport> reports)
    {
        _reports = reports.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReport? GetReport(string key)
        => _reports.TryGetValue(key, out var r) ? r : null;

    public IEnumerable<IReport> GetAll()
        => _reports.Values.OrderBy(r => r.Title);
}
