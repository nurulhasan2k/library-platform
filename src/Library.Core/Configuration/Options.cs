namespace Library.Core.Configuration;

// One options class per appsettings.json section. Every key is documented in docs/CONFIGURATION.md.
// Add your own keys here (and to both documents) when you need a new setting.

public sealed class LendingOptions
{
    public const string Section = "Lending";
    /// <summary>How many days a loan lasts. Brief: 14.</summary>
    public int LoanDays { get; set; } = 14;
    /// <summary>Fine per FULL day late. Brief: $0.50.</summary>
    public decimal FinePerDay { get; set; } = 0.50m;
}

public sealed class DatabaseOptions
{
    public const string Section = "Database";
    public string ConnectionString { get; set; } = "Data Source=data/library.db";
    /// <summary>Create demo data on first start when the database is empty.</summary>
    public bool SeedOnStartup { get; set; } = true;
}

public sealed class AuthOptions
{
    public const string Section = "Auth";
    public int MaxPinAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 5;
}

public sealed class KioskOptions
{
    public const string Section = "Kiosk";
    /// <summary>The branch this kiosk station belongs to (a Branch.Code, e.g. CENTRAL).</summary>
    public string BranchCode { get; set; } = "CENTRAL";
    public int IdleTimeoutSeconds { get; set; } = 60;
}

public sealed class NotificationOptions
{
    public const string Section = "Notifications";
    /// <summary>Send a "due soon" notice this many days before the due date.</summary>
    public int DueSoonDays { get; set; } = 2;
    /// <summary>Run the daily checks as a background job.</summary>
    public bool EnableScheduler { get; set; } = true;
}

public sealed class ReservationOptions
{
    public const string Section = "Reservations";
    public int MaxActiveHoldsPerPatron { get; set; } = 5;
    /// <summary>How many days a Ready hold is kept before it expires.</summary>
    public int ReadyHoldDays { get; set; } = 3;
}

public sealed class ApiOptions
{
    public const string Section = "Api";
    /// <summary>The API key clients send in the X-Api-Key header. Set it with user-secrets; never commit a real key.</summary>
    public string Key { get; set; } = "";
    public int RateLimitPerMinute { get; set; } = 60;
    public int MaxPageSize { get; set; } = 100;
    public bool EnableSwagger { get; set; } = true;
}

public sealed class ImportOptions
{
    public const string Section = "Import";
    public int MaxFileSizeMb { get; set; } = 5;
    public int MaxRows { get; set; } = 5000;
}

public sealed class ReportOptions
{
    public const string Section = "Reports";
    /// <summary>An item not borrowed for this many days counts as idle in the inventory-health report.</summary>
    public int IdleItemDays { get; set; } = 90;
}
