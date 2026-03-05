using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using Yonder.DataProvider;
using Yonder.Reports.Core;

namespace Yonder;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;

        // QuestPDF community licence – free for open-source / small commercial use.
        // Upgrade to LicenseType.Professional for commercial use above the free tier.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Connection string from appsettings.json "YonderDb", falls back to the
        // original hard-coded value so existing local dev environments keep working.
        var connectionString = Configuration.GetConnectionString("YonderDb")
            ?? @"Data Source=jdteasler\TeaslerDb;Initial Catalog=YonderDb;Integrated Security=True";

        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddControllers();   // needed for ReportsController

        services.AddDbContext<YonderContext>(options =>
            options.UseSqlServer(connectionString));

        // ── Report framework – zero-registration auto-discovery ─────────────
        // Any class implementing IReport in this assembly is found automatically.
        // To register a new report: just create the class. Nothing else required.
        var reportTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IReport).IsAssignableFrom(t));

        foreach (var reportType in reportTypes)
            services.AddScoped(typeof(IReport), reportType);

        services.AddScoped<ReportRegistry>();
        services.AddScoped<PdfExportService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();        // REST API: GET /api/reports/{key}/pdf
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });
    }
}
