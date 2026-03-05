using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Yonder.Reports.Core;

namespace Yonder.Controllers;

/// <summary>
/// REST API for PDF export.
///
/// GET /api/reports/{key}/pdf?params={jsonBase64}
///
/// The front-end encodes the parameters object as base64(JSON) and passes it
/// as a query string so no request body is needed for a simple GET download.
/// </summary>
[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportRegistry   _registry;
    private readonly PdfExportService _pdfExport;

    public ReportsController(ReportRegistry registry, PdfExportService pdfExport)
    {
        _registry  = registry;
        _pdfExport = pdfExport;
    }

    /// <summary>Returns a PDF for the given report key and parameters.</summary>
    /// <param name="key">Matches IReport.Key (e.g. "attendance-daily").</param>
    /// <param name="params">Base64-encoded JSON of the report's parameter object.</param>
    [HttpGet("{key}/pdf")]
    public async Task<IActionResult> DownloadPdf(
        [FromRoute] string key,
        [FromQuery(Name = "params")] string? @params = null)
    {
        var report = _registry.GetReport(key);
        if (report == null)
            return NotFound($"Report '{key}' is not registered.");

        object parameters;
        try
        {
            if (!string.IsNullOrWhiteSpace(@params))
            {
                var json = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(@params));
                parameters = JsonSerializer.Deserialize(json, report.ParametersType,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? Activator.CreateInstance(report.ParametersType)!;
            }
            else
            {
                parameters = Activator.CreateInstance(report.ParametersType)!;
            }
        }
        catch (Exception ex)
        {
            return BadRequest($"Could not parse parameters: {ex.Message}");
        }

        try
        {
            var pdf      = await _pdfExport.ExportAsync(key, parameters);
            var fileName = $"{key}-{DateTime.Today:yyyy-MM-dd}.pdf";
            return File(pdf, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"PDF generation failed: {ex.Message}");
        }
    }

    /// <summary>Returns metadata for all registered reports (useful for tooling / scripts).</summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        var result = _registry.GetAll().Select(r => new
        {
            r.Key,
            r.Title,
            r.Description,
            ParametersType = r.ParametersType.Name
        });
        return Ok(result);
    }
}
