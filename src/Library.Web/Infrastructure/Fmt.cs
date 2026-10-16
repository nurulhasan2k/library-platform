using System.Globalization;
using Library.Core.Contracts;

namespace Library.Web.Infrastructure;

/// <summary>Formatting helpers used by views so dates, money and labels look the same everywhere.</summary>
public static class Fmt
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public static string Date(DateOnly d) => d.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);
    public static string Date(DateOnly? d) => d is null ? "" : Date(d.Value);
    public static string Date(DateTime d) => d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    public static string DateTime_(DateTime d) => d.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture);
    public static string Money(decimal m) => m.ToString("C2", Us);
    public static string Status(ItemStatus s, bool inTransit = false) => inTransit ? "in transit" : s.ToString().ToLowerInvariant();
    public static string StatusClass(ItemStatus s, bool inTransit = false) => inTransit ? "warn" : s switch
    {
        ItemStatus.Available => "ok", ItemStatus.Borrowed => "info", ItemStatus.Damaged => "warn", _ => "bad"
    };
    public static string Hours(OpeningHours h) => h.IsClosed ? "Closed" : $"{h.Open:HH\\:mm} - {h.Close:HH\\:mm}";
    public static string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";
}
