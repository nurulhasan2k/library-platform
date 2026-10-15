using Library.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Library.Core.Services;

public static class PersonBServiceCollectionExtensions
{
    /// <summary>
    /// Registers Person B's REAL services: simulated clock, notifications, reservations, the daily scheduler and seeder.
    /// Call this BEFORE AddLibraryStubs() so the stubs are skipped.
    /// </summary>
    public static IServiceCollection AddLibraryPersonB(this IServiceCollection services)
    {
        services.TryAddSingleton<DemoLibraryStore>();

        services.AddSingleton<SimulatedClock>();
        services.AddSingleton<IClock>(sp => sp.GetRequiredService<SimulatedClock>());

        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationService>());

        services.AddSingleton<ReservationService>();
        services.AddSingleton<IReservationService>(sp => sp.GetRequiredService<ReservationService>());

        services.AddHostedService<ReservationAndNotificationSeeder>();
        services.AddHostedService<NotificationScheduler>();
        return services;
    }

    /// <summary>
    /// STAND-IN lending and card + PIN sign-in so the kiosk and the hooks work before Person A's services merge.
    /// Uses TryAdd, so if A's real ILendingService or IPatronAuthService is registered anywhere, the real one is used
    /// and these are ignored. Once A's real services exist, delete this call from Program.cs.
    /// </summary>
    public static IServiceCollection AddLibraryInMemoryLendingStubs(this IServiceCollection services)
    {
        services.TryAddSingleton<StubInMemoryLendingService>();
        services.TryAddSingleton<ILendingService>(sp => sp.GetRequiredService<StubInMemoryLendingService>());
        services.TryAddSingleton<IPatronAuthService, StubInMemoryPatronAuthService>();
        return services;
    }
}
