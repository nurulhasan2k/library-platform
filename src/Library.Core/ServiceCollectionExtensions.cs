using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Stubs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// Register a stub for every shared service that does not have a real implementation yet.
    /// Uses TryAdd, so a real implementation registered BEFORE this call wins. A real
    /// implementation registered AFTER this call also wins (last registration is the one resolved).
    /// </summary>
    public static IServiceCollection AddLibraryStubs(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<IBranchContext, StubBranchContext>();
        services.TryAddSingleton<INotificationService, StubNotificationService>();
        services.TryAddSingleton<IReservationService, StubReservationService>();
        services.TryAddSingleton<ICatalogueQuery, StubCatalogueQuery>();
        services.TryAddSingleton<IPatronAuthService, StubPatronAuthService>();
        services.TryAddSingleton<ILendingService, StubLendingService>();
        services.TryAddSingleton<ITransferService, StubTransferService>();
        return services;
    }
}
