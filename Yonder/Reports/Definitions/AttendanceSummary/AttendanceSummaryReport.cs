using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Yonder.DataProvider;
using Yonder.Reports.Core;
using Yonder.Reports.Core.Pdf;

namespace Yonder.Reports.Definitions.AttendanceSummary;

/// <summary>
/// Attendance Summary Report – per-class totals, averages, and a daily trend
/// table across a configurable date range.
/// </summary>
public sealed class AttendanceSummaryReport : ReportBase<DateRangeParameters, AttendanceSummaryData>
{
    public override string Key         => "attendance-summary";
    public override string Title       => "Attendance Summary Report";
    public override string Description => "Per-class totals, averages, and daily trend over a date range.";
    public override Type   PreviewComponent => typeof(AttendanceSummaryPreview);

    // ── Data fetching ─────────────────────────────────────────────────────────

    protected override async Task<AttendanceSummaryData> FetchDataAsync(
        DateRangeParameters parameters, IServiceProvider services)
    {
        var db    = services.GetRequiredService<YonderContext>();
        var start = parameters.StartDate.Date;
        var end   = parameters.EndDate.Date;

        var records = await (
            from rc in db.RollCall
            join c in db.Classes on rc.ClassId equals c.Id
            where rc.ClassDate.Date >= start && rc.ClassDate.Date <= end
            select new { c.ClassName, c.TeacherName, rc.ClassDate, rc.ClassSize }
        ).ToListAsync();

        var classRows = records
            .GroupBy(r => (r.ClassName, r.TeacherName))
            .Select(g => new ClassSummaryRow(
                ClassName:          g.Key.ClassName,
                TeacherName:        g.Key.TeacherName,
                Sessions:           g.Count(),
                TotalAttendance:    g.Sum(x => x.ClassSize),
                AverageAttendance:  g.Average(x => x.ClassSize)))
            .OrderBy(r => r.ClassName)
            .ToList();

        var dailyTotals = records
            .GroupBy(r => r.ClassDate.Date)
            .Select(g => new DailyTotalRow(g.Key, g.Sum(x => x.ClassSize)))
            .OrderBy(d => d.Date)
            .ToList();

        return new AttendanceSummaryData
        {
            StartDate   = start,
            EndDate     = end,
            ClassRows   = classRows,
            DailyTotals = dailyTotals
        };
    }

    // ── PDF layout ────────────────────────────────────────────────────────────

    protected override IDocument BuildDocument(AttendanceSummaryData data, DateRangeParameters parameters)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontColor(DocumentTheme.BodyText));

                // ── Header ───────────────────────────────────────────────────
                page.Header()
                    .ReportHeader(
                        title:    "Attendance Summary Report",
                        subtitle: $"{data.StartDate:MMM d, yyyy}  –  {data.EndDate:MMM d, yyyy}");

                // ── Body ─────────────────────────────────────────────────────
                page.Content()
                    .PaddingHorizontal(DocumentTheme.MarginPage)
                    .PaddingTop(DocumentTheme.SpacingLg)
                    .Column(col =>
                    {
                        // Summary stats
                        col.StatRow(
                            ("Total Attendance",  data.TotalAttendance.ToString("N0")),
                            ("Sessions Recorded", data.TotalSessions.ToString()),
                            ("Daily Average",     data.AverageDaily.ToString("N1")),
                            ("Peak Day",          data.PeakAttendance.ToString("N0")));

                        // Per-class summary
                        col.Section("Class Summary")
                           .DataTable(
                               data.ClassRows,
                               ("Class",              2.5f, r => r.ClassName),
                               ("Teacher",            2f,   r => r.TeacherName),
                               ("Sessions",           1f,   r => r.Sessions.ToString()),
                               ("Total",              1f,   r => r.TotalAttendance.ToString("N0")),
                               ("Avg / Session",      1f,   r => r.AverageAttendance.ToString("N1")));

                        if (data.ClassRows.Count > 0)
                        {
                            col.TotalsRow(
                                ("",      "",                                    2.5f),
                                ("",      "",                                    2f),
                                ("",      "",                                    1f),
                                ("Total", data.TotalAttendance.ToString("N0"),   1f),
                                ("",      "",                                    1f));
                        }

                        // Daily trend (only renders if data spans > 1 day)
                        if (data.DailyTotals.Count > 1)
                        {
                            col.Section("Daily Trend")
                               .DataTable(
                                   data.DailyTotals,
                                   ("Date",        2f, r => r.Date.ToString("ddd, MMM d, yyyy")),
                                   ("Attendance",  1f, r => r.Total.ToString("N0")));
                        }
                    });

                // ── Footer ───────────────────────────────────────────────────
                page.Footer()
                    .PaddingHorizontal(DocumentTheme.MarginPage)
                    .PaddingBottom(DocumentTheme.SpacingMd)
                    .ReportFooter();
            });
        });
    }

    // ── Preview wiring ────────────────────────────────────────────────────────

    public override Dictionary<string, object> BuildPreviewParameters(object data, object parameters)
        => new()
        {
            ["Data"]   = (AttendanceSummaryData)data,
            ["Params"] = (DateRangeParameters)parameters
        };
}
