using Library.Core;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Library.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library.Tests;

/// <summary>
/// A tiny test runner (no test framework needed, so the solution has no NuGet dependencies).
/// Run it with: dotnet run --project tests/Library.Tests
/// </summary>
public static class T
{
    private static int _passed;
    private static readonly List<string> Failures = new();

    public static void Test(string name, Action body)
    {
        try { body(); _passed++; Console.WriteLine($"  PASS  {name}"); }
        catch (Exception ex)
        {
            Failures.Add(name);
            Console.WriteLine($"  FAIL  {name}\n        {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void Section(string name) => Console.WriteLine($"\n{name}");
    public static void True(bool condition, string message = "Expected true") { if (!condition) throw new Exception(message); }
    public static void False(bool condition, string message = "Expected false") { if (condition) throw new Exception(message); }

    public static void Equal<TValue>(TValue expected, TValue actual, string? what = null)
    {
        if (!EqualityComparer<TValue>.Default.Equals(expected, actual))
            throw new Exception($"{what ?? "Values"} differ: expected <{expected}> but got <{actual}>");
    }

    public static void Rejects(string messageContains, Action action)
    {
        try { action(); }
        catch (LibraryException ex)
        {
            if (!ex.Message.Contains(messageContains, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Rejected, but the message was '{ex.Message}' (expected it to mention '{messageContains}')");
            return;
        }
        throw new Exception($"Expected a LibraryException mentioning '{messageContains}', but nothing was thrown");
    }

    public static int Finish()
    {
        Console.WriteLine();
        Console.WriteLine($"{_passed} passed, {Failures.Count} failed.");
        foreach (var failure in Failures) Console.WriteLine($"  - {failure}");
        return Failures.Count == 0 ? 0 : 1;
    }
}

public sealed class FakeBranchContext : IBranchContext
{
    public BranchInfo? CurrentBranch { get; set; }
    public int? CurrentDeskId { get; set; }
    public bool IsKiosk { get; set; }
}

/// <summary>A whole platform backed by a throw-away data file and a clock the tests control, seeded with the demo data.</summary>
public sealed class TestPlatform : IDisposable
{
    private readonly string _directory;
    public ServiceProvider Services { get; }
    public FixedClock Clock { get; }
    public FakeBranchContext Context { get; } = new();
    public ILibraryStore Store { get; }

    public TestPlatform(bool seed = true, DateOnly? today = null)
    {
        _directory = Path.Combine(Path.GetTempPath(), "platform-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Clock = new FixedClock(today ?? new DateOnly(2026, 10, 14)); // a Wednesday
        Store = new JsonLibraryStore(Path.Combine(_directory, "library.json"));

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddLibraryOptions(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:MaxPinAttempts"] = "3", ["Auth:LockoutMinutes"] = "5"
        }).Build());
        services.AddSingleton(Store);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IBranchContext>(Context);
        services.AddLibraryServices();
        Services = services.BuildServiceProvider();
        if (seed) Get<SeedRunner>().Seed();
    }

    public TService Get<TService>() where TService : notnull => Services.GetRequiredService<TService>();
    public LendingService Lending => Get<LendingService>();
    public InventoryService Inventory => Get<InventoryService>();
    public BorrowerService Borrowers => Get<BorrowerService>();

    public Branch Branch(string code) => Store.Read(d => d.Branches.First(b => b.Code == code));
    public Item Item(string code) => Store.Read(d => d.FindItemByCode(code)!);
    public Borrower Patron(string card) => Store.Read(d => d.Borrowers.First(b => b.CardNumber == card));
    public User User(string name) => Store.Read(d => d.Users.First(u => u.Username == name));

    public static TResult Wait<TResult>(Task<TResult> task) => task.GetAwaiter().GetResult();

    public void Dispose()
    {
        Services.Dispose();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }
}
