using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Yonder.Reports.Core.Pdf;

/// <summary>
/// Centralised design tokens for all PDF reports.
/// Update once here to restyle every report.
/// </summary>
public static class DocumentTheme
{
    // ── Colours ──────────────────────────────────────────────────────────────
    public static readonly string PrimaryColor   = "#1e40af"; // deep blue
    public static readonly string AccentColor    = "#3b82f6"; // mid blue
    public static readonly string HeaderBg       = "#1e40af";
    public static readonly string HeaderText     = Colors.White;
    public static readonly string AltRowBg       = "#f0f4ff";
    public static readonly string BorderColor    = "#cbd5e1";
    public static readonly string BodyText       = "#1e293b";
    public static readonly string MutedText      = "#64748b";

    // ── Typography ────────────────────────────────────────────────────────────
    public const float FontSizeTitle   = 22;
    public const float FontSizeH2      = 14;
    public const float FontSizeBody    = 10;
    public const float FontSizeSmall   = 8;
    public const float FontSizeCaption = 7;

    // ── Spacing ───────────────────────────────────────────────────────────────
    public const float MarginPage  = 40;   // points  (~14 mm)
    public const float SpacingSm  = 6;
    public const float SpacingMd  = 12;
    public const float SpacingLg  = 20;
}
