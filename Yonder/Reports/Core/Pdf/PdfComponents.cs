using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Yonder.Reports.Core.Pdf;

/// <summary>
/// Reusable QuestPDF layout helpers.
/// Compose these inside any report's BuildDocument method to keep
/// PDF layouts DRY and consistently styled.
/// </summary>
public static class PdfComponents
{
    // ── Page header / footer ─────────────────────────────────────────────────

    /// <summary>Standard full-width page header band with title and subtitle.</summary>
    public static void ReportHeader(this IContainer container, string title, string subtitle = "")
    {
        container
            .Background(DocumentTheme.HeaderBg)
            .PaddingHorizontal(DocumentTheme.MarginPage)
            .PaddingVertical(DocumentTheme.SpacingMd)
            .Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item()
                       .Text(title)
                       .FontSize(DocumentTheme.FontSizeTitle)
                       .FontColor(DocumentTheme.HeaderText)
                       .Bold();

                    if (!string.IsNullOrWhiteSpace(subtitle))
                        col.Item()
                           .Text(subtitle)
                           .FontSize(DocumentTheme.FontSizeBody)
                           .FontColor(DocumentTheme.HeaderText)
                           .Light();
                });

                row.ConstantItem(120).AlignRight().AlignMiddle()
                   .Text($"Generated {DateTime.Now:MMM d, yyyy}")
                   .FontSize(DocumentTheme.FontSizeSmall)
                   .FontColor(DocumentTheme.HeaderText)
                   .Light();
            });
    }

    /// <summary>Standard page footer with generation info and page numbers.</summary>
    public static void ReportFooter(this IContainer container)
    {
        container
            .BorderTop(1)
            .BorderColor(DocumentTheme.BorderColor)
            .PaddingTop(DocumentTheme.SpacingSm)
            .Row(row =>
            {
                row.RelativeItem()
                   .Text("Yonder Attendance System")
                   .FontSize(DocumentTheme.FontSizeCaption)
                   .FontColor(DocumentTheme.MutedText);

                row.RelativeItem().AlignRight()
                   .Text(text =>
                   {
                       text.Span("Page ").FontSize(DocumentTheme.FontSizeCaption).FontColor(DocumentTheme.MutedText);
                       text.CurrentPageNumber().FontSize(DocumentTheme.FontSizeCaption).FontColor(DocumentTheme.MutedText);
                       text.Span(" of ").FontSize(DocumentTheme.FontSizeCaption).FontColor(DocumentTheme.MutedText);
                       text.TotalPages().FontSize(DocumentTheme.FontSizeCaption).FontColor(DocumentTheme.MutedText);
                   });
            });
    }

    // ── Section / content ────────────────────────────────────────────────────

    /// <summary>Titled section container with optional bottom margin.</summary>
    public static IContainer Section(this ColumnDescriptor column, string heading)
    {
        column.Item().PaddingTop(DocumentTheme.SpacingLg)
            .Text(heading)
            .FontSize(DocumentTheme.FontSizeH2)
            .FontColor(DocumentTheme.PrimaryColor)
            .Bold();

        column.Item().Height(1).Background(DocumentTheme.AccentColor);

        return column.Item().PaddingTop(DocumentTheme.SpacingSm);
    }

    /// <summary>A row of summary stat boxes side by side.</summary>
    public static void StatRow(this ColumnDescriptor column, params (string Label, string Value)[] stats)
    {
        column.Item().PaddingTop(DocumentTheme.SpacingMd).Row(row =>
        {
            foreach (var (label, value) in stats)
            {
                row.RelativeItem()
                   .Border(1)
                   .BorderColor(DocumentTheme.BorderColor)
                   .Background(DocumentTheme.AltRowBg)
                   .Padding(DocumentTheme.SpacingMd)
                   .Column(col =>
                   {
                       col.Item()
                          .Text(value)
                          .FontSize(DocumentTheme.FontSizeH2 + 4)
                          .FontColor(DocumentTheme.PrimaryColor)
                          .Bold();
                       col.Item()
                          .Text(label)
                          .FontSize(DocumentTheme.FontSizeSmall)
                          .FontColor(DocumentTheme.MutedText);
                   });

                if (stats.Last() != (label, value))
                    row.ConstantItem(8); // gutter
            }
        });
    }

    // ── Tables ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders a generic data table.
    /// <paramref name="columns"/> defines (header text, relative width, value selector).
    /// </summary>
    public static void DataTable<T>(
        this IContainer container,
        IReadOnlyList<T> rows,
        params (string Header, float Width, Func<T, string> Value)[] columns)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                foreach (var col in columns)
                    def.RelativeColumn(col.Width);
            });

            // Header row
            table.Header(header =>
            {
                foreach (var col in columns)
                    header.Cell()
                          .Background(DocumentTheme.HeaderBg)
                          .Padding(DocumentTheme.SpacingSm)
                          .Text(col.Header)
                          .FontSize(DocumentTheme.FontSizeBody)
                          .FontColor(DocumentTheme.HeaderText)
                          .Bold();
            });

            // Data rows with zebra striping
            for (var i = 0; i < rows.Count; i++)
            {
                var bg = i % 2 == 1 ? DocumentTheme.AltRowBg : Colors.White;
                foreach (var col in columns)
                    table.Cell()
                         .Background(bg)
                         .BorderBottom(0.5f)
                         .BorderColor(DocumentTheme.BorderColor)
                         .Padding(DocumentTheme.SpacingSm)
                         .Text(col.Value(rows[i]))
                         .FontSize(DocumentTheme.FontSizeBody)
                         .FontColor(DocumentTheme.BodyText);
            }
        });
    }

    /// <summary>Renders a summary/totals row below a table.</summary>
    public static void TotalsRow(
        this ColumnDescriptor column,
        params (string Label, string Value, float Width)[] cells)
    {
        column.Item().Background(DocumentTheme.AltRowBg)
            .Border(1)
            .BorderColor(DocumentTheme.BorderColor)
            .PaddingVertical(DocumentTheme.SpacingSm)
            .Row(row =>
            {
                foreach (var (label, value, width) in cells)
                    row.RelativeItem(width)
                       .PaddingHorizontal(DocumentTheme.SpacingSm)
                       .Column(col =>
                       {
                           if (!string.IsNullOrEmpty(label))
                               col.Item().Text(label).FontSize(DocumentTheme.FontSizeSmall)
                                  .FontColor(DocumentTheme.MutedText);
                           col.Item().Text(value).FontSize(DocumentTheme.FontSizeBody)
                              .FontColor(DocumentTheme.PrimaryColor).Bold();
                       });
            });
    }
}
