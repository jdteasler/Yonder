using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Yonder.Reports.Core;

/// <summary>
/// Abstract base class every report definition extends.
/// Handles type-erased dispatch so implementations stay fully typed.
///
/// Usage – create one class per report:
/// <code>
/// public class MyReport : ReportBase&lt;MyParameters, MyData&gt;
/// {
///     public override string Key         => "my-report";
///     public override string Title       => "My Report";
///     public override string Description => "What this report shows.";
///     public override Type   PreviewComponent => typeof(MyReportPreview);
///
///     protected override async Task&lt;MyData&gt; FetchDataAsync(MyParameters p, IServiceProvider svc)
///     { … }
///
///     protected override IDocument BuildDocument(MyData data, MyParameters p)
///     { … return Document.Create(…); }
///
///     public override Dictionary&lt;string, object&gt; BuildPreviewParameters(object data, object parameters)
///         => new() { ["Data"] = (MyData)data, ["Params"] = (MyParameters)parameters };
/// }
/// </code>
/// </summary>
public abstract class ReportBase<TParams, TData> : IReport
    where TParams : ReportParameters, new()
{
    // ── IReport metadata ────────────────────────────────────────────────────

    public abstract string Key { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract Type PreviewComponent { get; }

    public Type ParametersType => typeof(TParams);

    // ── Strongly-typed contract (implement these in each report) ─────────────

    protected abstract Task<TData> FetchDataAsync(TParams parameters, IServiceProvider services);
    protected abstract IDocument BuildDocument(TData data, TParams parameters);

    // ── IReport type-erased dispatch ─────────────────────────────────────────

    async Task<object> IReport.FetchDataAsync(object parameters, IServiceProvider services)
        => (await FetchDataAsync((TParams)parameters, services))!;

    byte[] IReport.ExportPdf(object data, object parameters)
        => BuildDocument((TData)data, (TParams)parameters).GeneratePdf();

    public abstract Dictionary<string, object> BuildPreviewParameters(object data, object parameters);
}
