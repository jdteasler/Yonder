using QuestPDF.Fluent;

namespace Yonder.Reports.Core;

/// <summary>
/// Non-generic marker interface for auto-discovery and the registry.
/// All type-erased operations go through here so the registry and services
/// can work without knowing TParams / TData at compile time.
/// </summary>
public interface IReport
{
    /// <summary>URL-safe identifier, e.g. "attendance-daily".</summary>
    string Key { get; }

    /// <summary>Human-readable title shown in the catalog.</summary>
    string Title { get; }

    /// <summary>Short description shown in the catalog card.</summary>
    string Description { get; }

    /// <summary>The concrete parameter class for this report.</summary>
    Type ParametersType { get; }

    /// <summary>The Blazor component used to render the browser preview.</summary>
    Type PreviewComponent { get; }

    /// <summary>
    /// Fetches all data needed to render the report.
    /// <paramref name="parameters"/> is guaranteed to be an instance of ParametersType.
    /// </summary>
    Task<object> FetchDataAsync(object parameters, IServiceProvider services);

    /// <summary>
    /// Generates the raw PDF bytes for the report.
    /// Both arguments are the typed objects produced by FetchDataAsync.
    /// </summary>
    byte[] ExportPdf(object data, object parameters);

    /// <summary>
    /// Returns the DynamicComponent parameter dictionary to pass to the preview component.
    /// Keys must match the [Parameter] names on PreviewComponent exactly.
    /// </summary>
    Dictionary<string, object> BuildPreviewParameters(object data, object parameters);
}
