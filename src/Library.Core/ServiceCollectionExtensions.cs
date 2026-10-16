using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Import;
using Library.Core.Reports;
using Library.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Library.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Bind every options section from appsettings.json (see docs/CONFIGURATION.md).</summary>
    public static IServiceCollection AddLibraryOptions(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<LendingOptions>(config.GetSection(LendingOptions.Section));
        services.Configure<DatabaseOptions>(config.GetSection(DatabaseOptions.Section));
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
        services.Configure<KioskOptions>(config.GetSection(KioskOptions.Section));
        services.Configure<NotificationOptions>(config.GetSection(NotificationOptions.Section));
        services.Configure<ReservationOptions>(config.GetSection(ReservationOptions.Section));
        services.Configure<ApiOptions>(config.GetSection(ApiOptions.Section));
        services.Configure<ImportOptions>(config.GetSection(ImportOptions.Section));
        services.Configure<ReportOptions>(config.GetSection(ReportOptions.Section));
        return services;
    }

    /// <summary>
    /// Registers the data store and every service. IBranchContext is NOT registered here because it depends on the web
    /// request: the web project registers it.
    /// </summary>
    public static IServiceCollection AddLibraryServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILibraryStore>(sp =>
            new JsonLibraryStore(DataLocation.Resolve(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.DataFile)));
        services.TryAddSingleton<IClock, SimulatedClock>();

        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddSingleton<ReservationService>();
        services.AddSingleton<IReservationService>(sp => sp.GetRequiredService<ReservationService>());
        services.AddSingleton<TransferService>();
        services.AddSingleton<ITransferService>(sp => sp.GetRequiredService<TransferService>());
        services.AddSingleton<IPatronAuthService, PatronAuthService>();
        services.AddSingleton<ICatalogueQuery, CatalogueQuery>();

        services.AddSingleton<InventoryService>();
        services.AddSingleton<BorrowerService>();
        services.AddSingleton<StaffService>();
        services.AddSingleton<LoanQueryService>();

        // Lending reads the current desk/kiosk from IBranchContext, which is per request.
        services.AddScoped<LendingService>();
        services.AddScoped<ILendingService>(sp => sp.GetRequiredService<LendingService>());

        services.AddSingleton<IExternalCatalogueProvider, MockExternalProvider>();
        services.AddSingleton<ImportService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<SeedRunner>();
        return services;
    }
}
