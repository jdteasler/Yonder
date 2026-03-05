using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Yonder.DataProvider;
using Yonder.Reports.Core;
using Yonder.Reports.Core.Pdf;

namespace Yonder.Reports.Definitions.AttendanceDaily;

/// <summary>
/// Daily Attendance Report – shows every class's head-count for a single date.
///
/// This class is the single source-of-truth for this report:
///   • FetchDataAsync  – queries the database
///   • BuildDocument   – defines the PDF layout using QuestPDF
///   • PreviewComponent – points to the companion Blazor preview component
///
/// To add a brand-new report, copy this file + the companion Preview.razor,
/// rename everything, implement the three members above, and you're done.
/// No registration boilerplate required – the framework discovers it automatically.
/// </summary>
public sealed class AttendanceDailyReport : ReportBase<SingleDateParameters, AttendanceDailyData>
{
    public override string Key         => "attendance-daily";
    public override string Title       => "Daily Attendance Report";
    public override string Description => "Head-count per class for a selected date, with a daily total.";
    public override Type   PreviewComponent => typeof(AttendanceDailyPreview);

    // ── Data fetching ─────────────────────────────────────────────────────────

    protected override async Task<AttendanceDailyData> FetchDataAsync(
        SingleDateParameters parameters, IServiceProvider services)
    {
        var db = services.GetRequiredService<YonderContext>();

        // Normalise to date-only (strip time component) for comparison
        var targetDate = parameters.Date.Date;

        var rows = await (
            from rc in db.RollCall
            join c in db.Classes on rc.ClassId equals c.Id
            where rc.ClassDate.Date == targetDate
            orderby c.ClassName
            select new ClassAttendanceRow(rc.ClassId, c.ClassName, c.TeacherName, rc.ClassSize)
        ).ToListAsync();

        return new AttendanceDailyData
        {
            ReportDate = targetDate,
            Rows       = rows
        };
    }

    // ── PDF layout ────────────────────────────────────────────────────────────

    protected override IDocument BuildDocument(AttendanceDailyData data, SingleDateParameters parameters)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontColor(DocumentTheme.BodyText));

                // ── Header band ──────────────────────────────────────────────
                page.Header()
                    .ReportHeader(
                        title:    "Daily Attendance Report",
                        subtitle: $"Date: {data.ReportDate:MMMM d, yyyy}");

                // ── Body ─────────────────────────────────────────────────────
                page.Content()
                    .PaddingHorizontal(DocumentTheme.MarginPage)
                    .PaddingTop(DocumentTheme.SpacingLg)
                    .Column(col =>
                    {
                        // Summary stat row
                        col.StatRow(
                            ("Classes Active",  data.TotalClasses.ToString()),
                            ("Total Attendance", data.TotalAttendance.ToString()),
                            ("Report Date",      data.ReportDate.ToString("MMM d, yyyy")));

                        // Class breakdown table
                        col.Section("Class Breakdown")
                           .DataTable(
                               data.Rows,
                               ("Class",       2.5f, r => r.ClassName),
                               ("Teacher",     2f,   r => r.TeacherName),
                               ("Attendance",  1f,   r => r.ClassSize.ToString("N0")));

                        if (data.Rows.Count > 0)
                        {
                            col.TotalsRow(
                                ("",        "",                              2.5f),
                                ("",        "",                              2f),
                                ("Total",   data.TotalAttendance.ToString("N0"), 1f));
                        }
                        else
                        {
                            col.Item().PaddingTop(DocumentTheme.SpacingLg)
                               .AlignCenter()
                               .Text("No attendance records found for this date.")
                               .FontSize(DocumentTheme.FontSizeBody)
                               .FontColor(DocumentTheme.MutedText)
                               .Italic();
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
            ["Data"]   = (AttendanceDailyData)data,
            ["Params"] = (SingleDateParameters)parameters
        };
}
