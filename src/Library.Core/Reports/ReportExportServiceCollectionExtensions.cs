using Library.Core.Reports.Export;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Library.Core;

public static class ReportExportServiceCollectionExtensions
{
    private static readonly Lazy<bool> License = new(() =>
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        return true;
    });

    public static IServiceCollection AddLibraryReportExports(this IServiceCollection services)
    {
        _ = License.Value;
        services.TryAddTransient<IReportCsvExporter, ReportCsvExporter>();
        services.TryAddTransient<IReportPdfExporter, ReportPdfExporter>();
        return services;
    }
}
