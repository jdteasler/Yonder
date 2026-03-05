using System.ComponentModel.DataAnnotations;

namespace Yonder.Reports.Core;

/// <summary>Base class for all report parameter models.</summary>
public abstract class ReportParameters { }

/// <summary>Parameters for reports that operate on a single date.</summary>
public class SingleDateParameters : ReportParameters
{
    [Display(Name = "Report Date")]
    public DateTime Date { get; set; } = DateTime.Today;
}

/// <summary>Parameters for reports that operate over a date range.</summary>
public class DateRangeParameters : ReportParameters
{
    [Display(Name = "Start Date")]
    public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);

    [Display(Name = "End Date")]
    public DateTime EndDate { get; set; } = DateTime.Today;
}
