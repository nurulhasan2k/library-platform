using Library.Core.Reports;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class ReportScreenServiceCollectionExtensions
{
    public static IServiceCollection AddLibraryReportScreens(this IServiceCollection services)
    {
        // Fixed demonstration data until A registers the database report source.
        services.TryAddSingleton<IReportDataSource, SampleReportDataSource>();
        services.TryAddScoped<IReportService, ReportService>();
        services.AddScoped<Library.Web.Reports.ReportScreenService>();
        return services;
    }
}
