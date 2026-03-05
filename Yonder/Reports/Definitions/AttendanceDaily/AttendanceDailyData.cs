namespace Yonder.Reports.Definitions.AttendanceDaily;

/// <summary>All data needed to render the Daily Attendance report.</summary>
public class AttendanceDailyData
{
    public DateTime ReportDate { get; init; }

    /// <summary>One row per class that took attendance on the report date.</summary>
    public IReadOnlyList<ClassAttendanceRow> Rows { get; init; } = [];

    public int TotalAttendance => Rows.Sum(r => r.ClassSize);
    public int TotalClasses    => Rows.Count;
}

public record ClassAttendanceRow(
    int    ClassId,
    string ClassName,
    string TeacherName,
    int    ClassSize);
