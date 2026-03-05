namespace Yonder.Reports.Definitions.AttendanceSummary;

/// <summary>All data needed to render the Attendance Summary report.</summary>
public class AttendanceSummaryData
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate   { get; init; }

    /// <summary>Per-class summary across the date range.</summary>
    public IReadOnlyList<ClassSummaryRow> ClassRows { get; init; } = [];

    /// <summary>Daily totals trend data for the sparkline/table.</summary>
    public IReadOnlyList<DailyTotalRow> DailyTotals { get; init; } = [];

    public int TotalSessions   => DailyTotals.Count;
    public int TotalAttendance => ClassRows.Sum(r => r.TotalAttendance);
    public double AverageDaily => DailyTotals.Count > 0
        ? DailyTotals.Average(d => d.Total)
        : 0;
    public int PeakAttendance  => DailyTotals.Count > 0
        ? DailyTotals.Max(d => d.Total)
        : 0;
}

public record ClassSummaryRow(
    string ClassName,
    string TeacherName,
    int    Sessions,
    int    TotalAttendance,
    double AverageAttendance);

public record DailyTotalRow(DateTime Date, int Total);
